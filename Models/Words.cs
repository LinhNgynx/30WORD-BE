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
    }
}
