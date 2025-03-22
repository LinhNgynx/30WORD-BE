
using GeminiTest.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace GeminiTest.Data
{
    public class DataContext : IdentityDbContext<ApplicationUser>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<Word> Words { get; set; }
        public DbSet<Wordlist> Wordlists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Define relationship between User and Wordlist
            modelBuilder.Entity<Wordlist>()
                .HasOne(wl => wl.User)
                .WithMany() // A user can have multiple wordlists
                .HasForeignKey(wl => wl.UserId)
                .OnDelete(DeleteBehavior.Cascade); // If a user is deleted, delete their wordlists

            // Define relationship between Wordlist and Word
            modelBuilder.Entity<Word>()
                .HasOne(w => w.Wordlist)
                .WithMany(wl => wl.Words)
                .HasForeignKey(w => w.WordlistId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
