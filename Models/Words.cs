using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeminiTest.Models
{
    public class Word
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string WordText { get; set; }

        public string Phonetic { get; set; }
        public string PartOfSpeech { get; set; }
        public string EnglishMeaning { get; set; }
        public string VietnameseMeaning { get; set; }

        public string ExampleSentence { get; set; }

        // Foreign key reference to Wordlist
        [ForeignKey("Wordlist")]
        public int WordlistId { get; set; }
        public Wordlist Wordlist { get; set; }

        public List<Quiz> Quizzes { get; set; } = new();
        // ✅ Spaced repetition tracking
        public int CorrectStreak { get; set; } = 0;  // Consecutive correct answers
        public DateTime NextReviewDate { get; set; } = DateTime.UtcNow.AddDays(1);  // Initial review date
        public DateTime LastReviewDate { get; set; } = DateTime.MinValue;

        // ✅ Fluency Level (ONLY for display, NOT affecting Spaced Repetition logic)
        // ✅ Store Fluency as a column
        public int FluencyValue { get; set; } = 1; // Default to Beginner

        // ✅ Computed Fluency Level
        [NotMapped] // EF Core will NOT store this field
        public FluencyLevel Fluency
        {
            get => (FluencyLevel)FluencyValue;
            set => FluencyValue = (int)value;
        }
        public WordSentence WordSentence { get; set; }
    }
    public enum FluencyLevel
    {
        Beginner = 1,    // Needs frequent review (1 day)
        Familiar = 2,    // Review in 3 days
        Proficient = 3,  // Review in 7 days
        Advanced = 4,    // Review in 14 days
        Mastered = 5     // Review in 30+ days
    }
}
