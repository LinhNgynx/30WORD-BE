using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


[Route("api/gemini")]
[ApiController]
public class GeminiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly ILogger<GeminiController> _logger;

    public GeminiController(IHttpClientFactory httpClientFactory, IOptions<GeminiSettings> geminiSettings, ILogger<GeminiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = geminiSettings.Value.ApiKey;
        _logger = logger;
    }
   
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateContent([FromBody] WordRequest request)
    {
        if (request.Words == null || request.Words.Count == 0)
        {
            return BadRequest(new { error = "No words provided." });
        }

        string prompt = $@"
For each word, return a JSON array with:
- The **same 'id'** as in the input (do **not** generate a new ID).
- **word** and **phonetic transcription** (IPA notation preferred).
- **Up to 3 distinct Vietnamese meanings**, each labeled with its **part of speech**.
- Ensure meanings are short but still indicate **the word’s function** (e.g., noun, verb, adjective).
- If the word has multiple parts of speech, provide separate meanings for each.
- If the word is misspelled, invalid, or non-English, return an **error message** with a suggested correction or a note stating it's invalid.

**Input words:**  
{JsonSerializer.Serialize(request.Words, new JsonSerializerOptions { WriteIndented = true })}

**Response format:**
[
  {{
    ""id"": 1,
    ""word"": ""example"",
    ""phonetic"": ""/ɪɡˈzæm.pəl/"",
    ""meanings"": [
      {{ ""part_of_speech"": ""noun"", ""vietnamese_meaning"": ""Ví dụ minh họa"" }},
      {{ ""part_of_speech"": ""noun"", ""vietnamese_meaning"": ""Mẫu hình phổ biến"" }},
      {{ ""part_of_speech"": ""verb"", ""vietnamese_meaning"": ""Thể hiện đặc điểm"" }}
    ]
  }},
  {{
    ""id"": 2,
    ""word"": ""northside"",
    ""phonetic"": ""/ˈnɔːrθ.saɪd/"",
    ""meanings"": [
      {{ ""part_of_speech"": ""noun"", ""vietnamese_meaning"": ""Khu phía bắc"" }},
      {{ ""part_of_speech"": ""adjective"", ""vietnamese_meaning"": ""Thuộc khu phía bắc"" }}
    ]
  }},
  {{
    ""id"": 3,
    ""word"": ""mispeeledword"",
    ""meanings"": [
      {{ ""part_of_speech"": ""N/A"", ""vietnamese_meaning"": ""Không hợp lệ. Có phải bạn muốn: 'misspelledword'?"" }}
    ]
  }}
]
Edge Case: {{
    ""id"": ""same as input"",
    ""word"": ""A li cu boi"",
    ""meanings"": [
      {{
        ""part_of_speech"": ""noun"",
        ""vietnamese_meaning"": ""Mèo Bội mặt béo đáng yêu mún chết ""
      }}
    ]
  }}
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

            _logger.LogInformation("Sending request to Gemini API with words: {Words}", string.Join(", ", request.Words.Select(w => w.Word)));

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
        Id = Guid.NewGuid().ToString(), // Generate a unique ID for the error case
        Word = "Error",
        Phonetic = "N/A", // No phonetic available for an error response
        Meanings = new List<MeaningItem>
        {
            new MeaningItem
            {
                PartOfSpeech = "N/A",
                VietnameseMeaning = "Failed to parse response."
            }
        }
    }
};



    }

}

// Model for API request
public class WordRequest
{
    public List<WordItem> Words { get; set; }
}

public class WordItem
{
    public string Id { get; set; } // Fixed GUID for each row
    public string Word { get; set; }
}


// Model for vocabulary response
public class VocabularyItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("word")]
    public string Word { get; set; }

    [JsonPropertyName("phonetic")]
    public string Phonetic { get; set; } // Phonetic transcription

    [JsonPropertyName("meanings")]
    public List<MeaningItem> Meanings { get; set; } = new();
}

public class MeaningItem
{
    [JsonPropertyName("part_of_speech")]
    public string PartOfSpeech { get; set; }

    [JsonPropertyName("vietnamese_meaning")]
    public string VietnameseMeaning { get; set; }
}




// Strongly typed settings for Gemini API
public class GeminiSettings
{
    public string ApiKey { get; set; }
}
