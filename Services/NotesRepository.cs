using BotChar.Data;
using Microsoft.EntityFrameworkCore;

namespace BotChar.Services
{
    public class NotesRepository
    {
        private readonly AppDbContext _db;
        public NotesRepository(AppDbContext db) => _db = db;

        public async Task AddNoteAsync(Note note)
        {
            _db.Notes.Add(note);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Note>> GetNotesForPeriodAsync(long chatId, int days)
        {
            var now = DateTime.UtcNow;
            var max = now.AddDays(days);
            return await _db.Notes
                .Where(n => n.ChatId == chatId && n.ReminderUtc >= now && n.ReminderUtc <= max)
                .OrderBy(n => n.ReminderUtc)
                .ToListAsync();
        }

        public async Task<List<Note>> GetDueNotesAsync()
        {
            var now = DateTime.UtcNow;
            return await _db.Notes.Where(n => !n.IsSent && n.ReminderUtc <= now).ToListAsync();
        }

        public async Task DeleteNoteAsync(int id)
        {
            var n = await _db.Notes.FindAsync(id);
            if (n != null)
            {
                _db.Notes.Remove(n);
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkAsSentAsync(Note note)
        {
            note.IsSent = true;
            await _db.SaveChangesAsync();
        }
    }
}
