using System.ComponentModel.DataAnnotations;

namespace BotChar.Data
{
    public class UserSettings
    {
        [Key]
        public long ChatId { get; set; }
        public int TimezoneOffsetHours { get; set; } = 0;
    }
}
