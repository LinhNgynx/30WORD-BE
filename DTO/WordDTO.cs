using System.Text.Json.Serialization;

namespace GeminiTest.DTO

{
    public class WordDto
    {
        [JsonPropertyName("word")]
        public string Word { get; set; }

        [JsonPropertyName("phonetic")]
        public string Phonetic { get; set; }

        [JsonPropertyName("part_of_speech")]
        public string PartOfSpeech { get; set; }

        [JsonPropertyName("english_meaning")]
        public string EnglishMeaning { get; set; }

        [JsonPropertyName("vietnamese_meaning")]
        public string VietnameseMeaning { get; set; }

        [JsonPropertyName("example")]
        public string Example { get; set; }
    }

}
