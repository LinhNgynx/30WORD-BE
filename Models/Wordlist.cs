using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace GeminiTest.Models
{
    public class Wordlist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }  // GUID stored as a string

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }  // Navigation property

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Wordlist name (e.g., "Daily Vocabulary")

        [MaxLength(500)]
        public string Description { get; set; } // Optional description

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Word> Words { get; set; } = new();
    }
}
