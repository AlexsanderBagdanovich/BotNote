using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotChar.Utils
{
    public class TelegramSender
    {
        private readonly ITelegramBotClient _client;

        public TelegramSender(ITelegramBotClient client)
        {
            _client = client;
        }

        // ✅ Новый метод GetMe()
        public Task<User> GetMeAsync(CancellationToken cancellationToken = default)
            => _client.GetMe(cancellationToken);

        // ✅ Новый SendMessage вместо SendTextMessageAsync
        public Task<Message> SendTextAsync(
            long chatId,
            string text,
            InlineKeyboardMarkup? replyMarkup = null,
            CancellationToken cancellationToken = default)
        {
            return _client.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken
            );
        }

        // ✅ Новый AnswerCallbackQuery вместо AnswerCallbackQueryAsync
        public Task AnswerCallbackQueryAsync(
            string callbackQueryId,
            string? text = null,
            bool showAlert = false,
            CancellationToken cancellationToken = default)
        {
            return _client.AnswerCallbackQuery(
                callbackQueryId: callbackQueryId,
                text: text,
                showAlert: showAlert,
                cancellationToken: cancellationToken
            );
        }
    }
}
