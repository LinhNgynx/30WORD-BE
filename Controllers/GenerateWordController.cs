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
- 'word' contains the original word followed by an appropriate emoji representing its meaning.
- 'part_of_speech' is inferred from the Vietnamese meaning.
- 'english_meaning' is a **concise, summarized definition** that accurately matches the given Vietnamese meaning(NOT THE VIETNAMESE MEANING YOU THINK IT IS).
- 'phonetic' contains the IPA transcription.
- 'vietnamese_meaning' remains unchanged.
- 'example' provides a natural English sentence using the word correctly in its given meaning.

**Important Rules:**
- Do not change the Vietnamese meaning.
- 'english_meaning' must be a clear, precise, concise definition that **exactly** corresponds to the provided Vietnamese meaning, even if the word has multiple meanings. DO NOT default to common meanings that do not match the Vietnamese meaning.
- Keep the word order the same as in the input.
- Choose an emoji that visually represents the word's meaning and place it immediately after the word.

### Example Input:
[
  {{ ""word"": ""comprehensive"", ""vietnamese_meaning"": ""toàn diện"" }},
  {{ ""word"": ""negligence"", ""vietnamese_meaning"": ""sự cẩu thả"" }},
  {{  ""word"": ""shabby"", ""vietnamese_meaning"": ""hèn hạ, đáng khinh"" }}
]

### Example Output:
[
  {{
    ""word"": ""comprehensive 📖"",
    ""part_of_speech"": ""adjective"",
    ""english_meaning"": ""covering all aspects of something"",
    ""phonetic"": ""/ˌkɒm.prɪˈhen.sɪv/"",
    ""vietnamese_meaning"": ""toàn diện"",
    ""example"": ""The report provides a comprehensive analysis of the issue.""
  }},
  {{
    ""word"": ""negligence ⚠️"",
    ""part_of_speech"": ""noun"",
    ""english_meaning"": ""failure to take proper care"",
    ""phonetic"": ""/ˈnɛɡ.lɪ.dʒəns/"",
    ""vietnamese_meaning"": ""sự cẩu thả"",
    ""example"": ""His negligence led to a serious accident.""
  }},
  {{
    ""word"": ""shabby 😠"",
    ""part_of_speech"": ""adjective"",
    ""english_meaning"": ""dishonorable and despicable"",
    ""phonetic"": ""/ˈʃæb.i/"",
    ""vietnamese_meaning"": ""hèn hạ, đáng khinh"",
    ""example"": ""His shabby actions made everyone lose respect for him.""
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
