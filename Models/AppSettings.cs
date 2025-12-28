namespace BotChar.Models
{
    public class AppSettings
    {
        public string BotToken { get; set; } = "";
        public DatabaseSettings Database { get; set; } = new();
        public int DefaultTimezoneOffsetHours { get; set; } = 3;
        public int ReminderCheckIntervalSeconds { get; set; } = 60;
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "";
    }
}
