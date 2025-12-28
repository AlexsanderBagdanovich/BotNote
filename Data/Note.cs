namespace BotChar.Data
{
    public class Note
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public DateTime ReminderUtc { get; set; }
        public string Text { get; set; } = "";
        public int TimezoneOffsetHours { get; set; } = 0;
        public bool IsSent { get; set; } = false;
    }
}
