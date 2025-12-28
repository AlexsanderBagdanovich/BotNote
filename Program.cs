using BotChar.Data;
using BotChar.Models;
using BotChar.Services;
using BotChar.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// bind settings
builder.Services.Configure<AppSettings>(builder.Configuration);
var settings = builder.Configuration.Get<AppSettings>()!;

// EF Core
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(settings.Database.ConnectionString));

// Telegram client
builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(settings.BotToken));

// TelegramSender
builder.Services.AddSingleton<TelegramSender>();

// repos & services
builder.Services.AddScoped<NotesRepository>();
builder.Services.AddScoped<UserSettingsRepository>();
builder.Services.AddHostedService<ReminderService>();
builder.Services.AddSingleton<BotService>();

var host = builder.Build();

// ensure db
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Start bot service (polling)
using (var scope = host.Services.CreateScope())
{
    var bot = scope.ServiceProvider.GetRequiredService<BotService>();
    await bot.StartAsync(CancellationToken.None);
    Console.WriteLine("BotChar started. Press Ctrl+C to exit.");
}

await host.RunAsync();
