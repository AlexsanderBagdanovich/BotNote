using BotChar.Data;

namespace BotChar.Services
{
    public class UserSettingsRepository
    {
        private readonly AppDbContext _db;
        public UserSettingsRepository(AppDbContext db) => _db = db;

        public async Task<int> GetTimezoneOffsetAsync(long chatId, int defaultOffset)
        {
            var user = await _db.UserSettings.FindAsync(chatId);
            return user?.TimezoneOffsetHours ?? defaultOffset;
        }

        public async Task SetTimezoneOffsetAsync(long chatId, int offset)
        {
            var user = await _db.UserSettings.FindAsync(chatId);
            if (user == null)
            {
                user = new UserSettings { ChatId = chatId, TimezoneOffsetHours = offset };
                _db.UserSettings.Add(user);
            }
            else
            {
                user.TimezoneOffsetHours = offset;
            }
            await _db.SaveChangesAsync();
        }
    }
}
