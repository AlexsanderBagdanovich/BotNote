using Microsoft.EntityFrameworkCore;

namespace BotChar.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<UserSettings> UserSettings => Set<UserSettings>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserSettings>().HasKey(u => u.ChatId);
            base.OnModelCreating(modelBuilder);
        }
    }
}
