using BotChar.Models;
using BotChar.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace BotChar.Services
{
    public class ReminderService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly TelegramSender _sender;
        private readonly ILogger<ReminderService> _logger;
        private readonly AppSettings _settings;

        public ReminderService(IServiceProvider services, TelegramSender sender, ILogger<ReminderService> logger, IOptions<AppSettings> options)
        {
            _services = services;
            _sender = sender;
            _logger = logger;
            _settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.ReminderCheckIntervalSeconds));
            _logger.LogInformation("ReminderService started, interval {Interval}s", interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var notesRepo = scope.ServiceProvider.GetRequiredService<NotesRepository>();

                    var due = await notesRepo.GetDueNotesAsync();
                    foreach (var n in due)
                    {
                        try
                        {
                            var local = n.ReminderUtc + TimeSpan.FromHours(n.TimezoneOffsetHours);
                            await _sender.SendTextAsync(n.ChatId, $"🔔 Напоминание: {n.Text}\n📅 {local:dd.MM.yyyy HH:mm}", cancellationToken: stoppingToken);
                            await notesRepo.MarkAsSentAsync(n);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send reminder for note {Id}", n.Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error in ReminderService loop");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
