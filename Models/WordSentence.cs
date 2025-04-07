using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GeminiTest.Models
{
    public class WordSentence
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SentenceText { get; set; }  // The sentence created by the user

        public string Feedback { get; set; }  // AI or instructor feedback

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [ForeignKey("WordId")]
        
        public int WordId { get; set; }
        [JsonIgnore]
        public Word Word { get; set; }
    }
}