using BotChar.Data;
using BotChar.Models;
using BotChar.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;

namespace BotChar.Services
{
    public class BotService
    {
        private readonly ITelegramBotClient _client;
        private readonly TelegramSender _sender;
        private readonly IServiceProvider _services;
        private readonly ILogger<BotService> _logger;
        private readonly AppSettings _settings;

        private readonly ConcurrentDictionary<long, DialogState> _states = new();

        public BotService(ITelegramBotClient client, TelegramSender sender, IServiceProvider services, ILogger<BotService> logger, IOptions<AppSettings> options)
        {
            _client = client;
            _sender = sender;
            _services = services;
            _logger = logger;
            _settings = options.Value;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            var me = await _sender.GetMeAsync(ct);
            _logger.LogInformation("Bot @{Username} starting", me.Username);

            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _client.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions, ct);
            _logger.LogInformation("StartReceiving called");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Type == UpdateType.Message && update.Message?.Text != null)
                {
                    await HandleMessage(update.Message, ct);
                    return;
                }

                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    await HandleCallback(update.CallbackQuery, ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleUpdateAsync error");
            }
        }

        private Task SendMenuAsync(long chatId, CancellationToken ct)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] {
                    InlineKeyboardButton.WithCallbackData("➕ Добавить","add"),
                    InlineKeyboardButton.WithCallbackData("📋 Список","list")
                },
                new[] {
                    InlineKeyboardButton.WithCallbackData("🗑 Удалить","delete"),
                    InlineKeyboardButton.WithCallbackData("⚙️ Часовой пояс","tz")
                }
            });

            return _sender.SendTextAsync(chatId, "Выберите действие:", keyboard, cancellationToken: ct);
        }

        private async Task HandleMessage(Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var text = message.Text!.Trim();

            using var scope = _services.CreateScope();
            var notesRepo = scope.ServiceProvider.GetRequiredService<NotesRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<UserSettingsRepository>();

            var tz = await userRepo.GetTimezoneOffsetAsync(chatId, _settings.DefaultTimezoneOffsetHours);

            var state = _states.GetOrAdd(chatId, _ => new DialogState());

            if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                await SendMenuAsync(chatId, ct);
                state.Reset();
                return;
            }

            if (text.Equals("/add", StringComparison.OrdinalIgnoreCase))
            {
                state.Stage = DialogStage.AwaitingDateTimeForAdd;
                await _sender.SendTextAsync(chatId, "Введите дату (ДД.MM.ГГГГ) или дату+время (ДД.MM.ГГГГ ЧЧ:ММ):", cancellationToken: ct);
                return;
            }

            switch (state.Stage)
            {
                case DialogStage.AwaitingDateTimeForAdd:
                    {
                        if (DateTime.TryParseExact(text, new[] { "dd.MM.yyyy HH:mm", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            if (text.Length == 10) // only date
                            {
                                state.TempLocalDate = dt.Date;
                                state.Stage = DialogStage.AwaitingTimeForAdd;
                                await _sender.SendTextAsync(chatId, "Укажите время (ЧЧ:ММ), например 22:00:", cancellationToken: ct);
                            }
                            else
                            {
                                state.TempLocalDate = dt;
                                state.Stage = DialogStage.AwaitingTextForAdd;
                                await _sender.SendTextAsync(chatId, "Введите текст заметки:", cancellationToken: ct);
                            }
                        }
                        else
                        {
                            await _sender.SendTextAsync(chatId, "Неверный формат даты. Используйте ДД.MM.ГГГГ или ДД.MM.ГГГГ ЧЧ:ММ", cancellationToken: ct);
                        }
                        return;
                    }

                case DialogStage.AwaitingTimeForAdd:
                    {
                        if (TimeSpan.TryParseExact(text, "hh\\:mm", CultureInfo.InvariantCulture, out var time))
                        {
                            state.TempLocalDate = state.TempLocalDate.Date + time;
                            state.Stage = DialogStage.AwaitingTextForAdd;
                            await _sender.SendTextAsync(chatId, "Введите текст заметки:", cancellationToken: ct);
                        }
                        else
                        {
                            await _sender.SendTextAsync(chatId, "Неверный формат времени. Используйте ЧЧ:ММ", cancellationToken: ct);
                        }
                        return;
                    }

                case DialogStage.AwaitingTextForAdd:
                    {
                        var local = state.TempLocalDate;
                        var utc = local - TimeSpan.FromHours(tz);

                        var note = new Note
                        {
                            ChatId = chatId,
                            ReminderUtc = utc,
                            Text = text,
                            TimezoneOffsetHours = tz,
                            IsSent = false
                        };

                        await notesRepo.AddNoteAsync(note);
                        state.Reset();

                        await _sender.SendTextAsync(chatId, $"✅ Заметка добавлена: \"{note.Text}\" на {local:dd.MM.yyyy HH:mm} (UTC{(tz >= 0 ? "+" : "")}{tz})", cancellationToken: ct);
                        return;
                    }

                case DialogStage.AwaitingCustomDaysForList:
                    {
                        if (int.TryParse(text, out var days) && days > 0)
                        {
                            var notes = await notesRepo.GetNotesForPeriodAsync(chatId, days);
                            await _sender.SendTextAsync(chatId, FormatNotes(notes), cancellationToken: ct);
                        }
                        else
                        {
                            await _sender.SendTextAsync(chatId, "Неверное число. Введите положительное целое.", cancellationToken: ct);
                        }
                        state.Reset();
                        return;
                    }

                case DialogStage.AwaitingTimezone:
                    {
                        var s = text.Trim();
                        if ((s.StartsWith("+") || s.StartsWith("-")) && int.TryParse(s.Replace("+", ""), out var off) || int.TryParse(s, out off))
                        {
                            await userRepo.SetTimezoneOffsetAsync(chatId, off);
                            await _sender.SendTextAsync(chatId, $"Часовой пояс установлен: UTC{(off >= 0 ? "+" : "")}{off}", cancellationToken: ct);
                        }
                        else
                        {
                            await _sender.SendTextAsync(chatId, "Неверный формат. Пример: +3 или -5 или 0", cancellationToken: ct);
                        }
                        state.Reset();
                        return;
                    }
            }

            await _sender.SendTextAsync(chatId, "Команды:\n/start — меню\n/add — добавить заметку\n/list — показать заметки\n/settz — установить часовой пояс", cancellationToken: ct);
        }

        private async Task HandleCallback(CallbackQuery callback, CancellationToken ct)
        {
            var chatId = callback.Message!.Chat.Id;
            var data = callback.Data!;
            using var scope = _services.CreateScope();
            var notesRepo = scope.ServiceProvider.GetRequiredService<NotesRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<UserSettingsRepository>();

            var tz = await userRepo.GetTimezoneOffsetAsync(chatId, _settings.DefaultTimezoneOffsetHours);
            var state = _states.GetOrAdd(chatId, _ => new DialogState());

            if (data == "add")
            {
                state.Stage = DialogStage.AwaitingDateTimeForAdd;
                await _sender.SendTextAsync(chatId, "Введите дату (ДД.MM.ГГГГ) или дату+время (ДД.MM.ГГГГ ЧЧ:ММ):", cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, text: "Добавление", cancellationToken: ct);
                return;
            }

            if (data == "list")
            {
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("📅 За неделю","list_7"), InlineKeyboardButton.WithCallbackData("📅 За месяц","list_30") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔢 Свой период","list_custom") }
                });
                await _sender.SendTextAsync(chatId, "Выберите период:", keyboard, cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                return;
            }

            if (data == "list_7" || data == "list_30")
            {
                var days = data == "list_7" ? 7 : 30;
                var notes = await notesRepo.GetNotesForPeriodAsync(chatId, days);
                await _sender.SendTextAsync(chatId, FormatNotes(notes), cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, text: $"Показано за {days} дней", cancellationToken: ct);
                return;
            }

            if (data == "list_custom")
            {
                state.Stage = DialogStage.AwaitingCustomDaysForList;
                await _sender.SendTextAsync(chatId, "Введите количество дней (например: 10):", cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                return;
            }

            if (data == "delete")
            {
                var notes = await notesRepo.GetNotesForPeriodAsync(chatId, 3650);
                if (!notes.Any())
                {
                    await _sender.SendTextAsync(chatId, "Нет заметок для удаления.", cancellationToken: ct);
                    await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                    return;
                }

                var buttons = notes.Select(n => new[] {
                    InlineKeyboardButton.WithCallbackData($"{(n.ReminderUtc + TimeSpan.FromHours(n.TimezoneOffsetHours)):dd.MM.yyyy HH:mm} — {Trunc(n.Text, 30)}", $"delete_{n.Id}")
                }).ToArray();

                await _sender.SendTextAsync(chatId, "Выберите заметку для удаления:", new InlineKeyboardMarkup(buttons), cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                return;
            }

            if (data != null && data.StartsWith("delete_"))
            {
                var id = int.Parse(data.Split('_')[1]);
                await notesRepo.DeleteNoteAsync(id);
                await _sender.SendTextAsync(chatId, "✅ Заметка удалена.", cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                return;
            }

            if (data == "tz")
            {
                state.Stage = DialogStage.AwaitingTimezone;
                await _sender.SendTextAsync(chatId, "Введите смещение часового пояса (пример: +3, -5, 0):", cancellationToken: ct);
                await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
                return;
            }

            await _sender.AnswerCallbackQueryAsync(callbackQueryId: callback.Id, cancellationToken: ct);
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

        private string FormatNotes(System.Collections.Generic.List<Note> notes)
        {
            if (notes == null || notes.Count == 0) return "📭 Нет заметок на указанный период.";
            var sb = new StringBuilder();
            foreach (var n in notes)
            {
                var local = n.ReminderUtc + TimeSpan.FromHours(n.TimezoneOffsetHours);
                sb.AppendLine($"📅 {local:dd.MM.yyyy HH:mm} — {n.Text} (id:{n.Id})");
            }
            return sb.ToString();
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            _logger.LogError(ex, "Polling error");
            return Task.CompletedTask;
        }

        private class DialogState
        {
            public DialogStage Stage { get; set; } = DialogStage.None;
            public DateTime TempLocalDate { get; set; }
            public void Reset() { Stage = DialogStage.None; TempLocalDate = default; }
        }

        private enum DialogStage
        {
            None,
            AwaitingDateTimeForAdd,
            AwaitingTimeForAdd,
            AwaitingTextForAdd,
            AwaitingCustomDaysForList,
            AwaitingTimezone
        }
    }
}
