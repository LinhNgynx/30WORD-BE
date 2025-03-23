using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenerateWordController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;
        private readonly ILogger<GenerateWordController> _logger;

        public GenerateWordController(IHttpClientFactory httpClientFactory, IOptions<GeminiSettings> geminiSettings, ILogger<GenerateWordController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = geminiSettings.Value.ApiKey;
            _logger = logger;
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("🚨 Gemini API Key is missing! Please check configuration.");
            }
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] Dictionary<string, string> selectedMeanings)
        {
            if (selectedMeanings == null || selectedMeanings.Count == 0)
            {
                return BadRequest(new { error = "No meanings provided." });
            }

            // 🔹 Extract words from selectedMeanings
            var wordList = selectedMeanings.Select(kv => new { Word = kv.Key, VietnameseMeaning = kv.Value }).ToList();

            string prompt = $@"
For each word provided along with its Vietnamese meaning, return a JSON array where:
- The 'word' field contains the original word.
- The 'part_of_speech' field corresponds to the given Vietnamese meaning.
- The 'english_meaning' field provides the correct English definition that matches the given Vietnamese meaning.
- The 'phonetic' field contains the IPA transcription of the word.
- The 'vietnamese_meaning' field remains unchanged.
- The 'example' field contains an English sentence using the word appropriately in the given part of speech.

**Important:**
- Do not change the Vietnamese meaning.
- Determine the correct part of speech based on the Vietnamese meaning.
- Ensure the English meaning aligns with the given Vietnamese meaning.
- Keep the word order the same as in the input.

### Example Input:
[
  {{ ""word"": ""struggle"", ""vietnamese_meaning"": ""sự đấu tranh"" }},
  {{ ""word"": ""run"", ""vietnamese_meaning"": ""chạy"" }}
]

### Example Output:
[
  {{
    ""word"": ""struggle"",
    ""part_of_speech"": ""noun"",
    ""english_meaning"": ""a difficult effort or fight"",
    ""phonetic"": ""/ˈstrʌɡ.əl/"",
    ""vietnamese_meaning"": ""sự đấu tranh"",
    ""example"": ""The struggle for freedom is never easy.""
  }},
  {{
    ""word"": ""run"",
    ""part_of_speech"": ""verb"",
    ""english_meaning"": ""to move quickly on foot"",
    ""phonetic"": ""/rʌn/"",
    ""vietnamese_meaning"": ""chạy"",
    ""example"": ""She runs every morning in the park.""
  }}
]

Now, process the following words:

{JsonSerializer.Serialize(wordList, new JsonSerializerOptions { WriteIndented = true })}

Ensure the output JSON maintains the same order as the input.
";


            var requestBody = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash:generateContent?key={_apiKey}";

                _logger.LogInformation("Sending request to Gemini API with words: {Words}", string.Join(", ", wordList.Select(w => w.Word)));
                _logger.LogInformation("Using Gemini API Key: {ApiKey}", _apiKey);

                var response = await httpClient.PostAsync(apiUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error: {StatusCode} - {Message}", response.StatusCode, errorMessage);
                    return StatusCode((int)response.StatusCode, new { error = "Error calling Gemini API.", details = errorMessage });
                }

                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Raw Gemini API Response: {Response}", responseString);

                var extractedJson = ExtractJsonFromResponse(responseString);

                return Ok(extractedJson);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError("HTTP request error: {Message}", httpEx.Message);
                return StatusCode(500, new { error = "Internal server error", details = httpEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }

        private List<VocabularyItem> ExtractJsonFromResponse(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    string contentText = textElement.GetString() ?? "";

                    // 🔹 Remove Markdown formatting (` ```json ... ``` `) and trim
                    contentText = contentText.Replace("```json", "").Replace("```", "").Trim();

                    _logger.LogInformation("Extracted JSON: {ExtractedJson}", contentText);

                    return JsonSerializer.Deserialize<List<VocabularyItem>>(contentText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<VocabularyItem>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error extracting JSON from Gemini response: {Message}", ex.ToString());
            }

            return new List<VocabularyItem>
            {
                new VocabularyItem
                {
                    Word = "Error",
                    Phonetic = "N/A",
                    PartOfSpeech = "N/A",
                    EnglishMeaning = "Failed to parse response.",
                    VietnameseMeaning = "Lỗi phân tích phản hồi.",
                    Example = "N/A"
                }
            };
        }
    }

    // ✅ Model for vocabulary response
    public class VocabularyItem
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

    // ✅ Strongly typed settings for Gemini API
    
}
