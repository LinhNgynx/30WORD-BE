using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using System.Linq;  // Needed for shuffling

namespace GeminiTest.Models
{
    public class Quiz
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WordId { get; set; }

        [ForeignKey("WordId")]
        public Word Word { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(20)")]
        public QuizType Type { get; set; }

        [Required]
        public string Question { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string OptionsJson { get; set; }  // Store options as JSON

        [Required]
        public string CorrectAnswer { get; set; }  // Store actual correct answer text

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        [NotMapped]
        public List<string> Options
        {
            get
            {
                var options = JsonConvert.DeserializeObject<List<string>>(OptionsJson);
                return options.OrderBy(_ => Guid.NewGuid()).ToList();  // Shuffle when retrieving
            }
            set => OptionsJson = JsonConvert.SerializeObject(value);
        }
    }

    public enum QuizType
    {
        Meaning,
        ContextUsage,
        SynonymAntonym
    }
}
