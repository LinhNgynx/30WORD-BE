using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenerateQuizController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;
        private readonly ILogger<GenerateQuizController> _logger;

        public GenerateQuizController(IHttpClientFactory httpClientFactory, IOptions<GeminiSettings> geminiSettings, ILogger<GenerateQuizController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = geminiSettings.Value.ApiKey;
            _logger = logger;
        }
        [HttpPost("generate/meaningquiz")]
        public async Task<IActionResult> GenerateContent([FromBody] GenerateQuizRequest request)
        {
            if (request.Words == null || request.Words.Count == 0)
            {
                return BadRequest(new { error = "No words provided." });
            }

            string prompt = $@"
Create a JSON array of quiz questions based on the given words and their meanings. 

Each quiz question should follow this structure:
- 'wordId': The original ID of the word.
- 'question': A clear, engaging multiple-choice question about the word’s meaning.
- 'options': A list of four possible answers, including the correct one.
- 'correctAnswer': The correct answer.

**Rules for Generating Options:**
- The **correct meaning** should always be included.
- The **three distractors** should be:
  - Plausible but incorrect meanings.
  - Unique across different words to avoid repetition.
  - Aligned with real-world misunderstandings or common mix-ups.
  - Different in concept from the correct answer while still making sense.

**Example Input:**
[
  {{ ""id"": 407, ""wordText"": ""hot"", ""englishMeaning"": ""having a high degree of heat or a high temperature."" }},
  {{ ""id"": 408, ""wordText"": ""espresso"", ""englishMeaning"": ""coffee brewed by forcing hot water through finely ground coffee beans."" }}
]

**Example Output:**
[
  {{
    ""wordId"": 407,
    ""question"": ""What does the word 'hot' mean in this context?"",
    ""options"": [
      ""having a high degree of heat or a high temperature."",
      ""a popular dance move from the 90s."",
      ""a moment of sudden realization or enlightenment."",
      ""a type of deep-sea fish known for bioluminescence.""
    ],
    ""correctAnswer"": ""having a high degree of heat or a high temperature.""
  }},
  {{
    ""wordId"": 408,
    ""question"": ""Which of the following best defines 'espresso'?"",
    ""options"": [
      ""coffee brewed by forcing hot water through finely ground coffee beans."",
      ""a strong alcoholic drink made from fermented wheat."",
      ""a small, single-engine aircraft used for short-distance flights."",
      ""a method of writing used in ancient Greece.""
    ],
    ""correctAnswer"": ""coffee brewed by forcing hot water through finely ground coffee beans.""
  }}
]

Now, generate quizzes for the following words:

{JsonSerializer.Serialize(request.Words, new JsonSerializerOptions { WriteIndented = true })}
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

                _logger.LogInformation("Sending request to Gemini API with words: {Words}", string.Join(", ", request.Words.Select(w => w.WordText)));

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

        [HttpPost("generate/contextquiz")]
        public async Task<IActionResult> GenerateContext([FromBody] GenerateQuizRequest request)
        {
            if (request.Words == null || request.Words.Count == 0)
            {
                return BadRequest(new { error = "No words provided." });
            }

            string prompt = $@"
You're designing a **fun and educational quiz** that helps users learn words by showing them in **real-world contexts**.  
Each question should be structured as follows:

- 'wordId': The original ID of the word.
- 'question': A **natural, real-life situation where the word is used in context**.  
- 'options': Four **plausible** multiple-choice answers, including the correct word and three well-thought-out distractors.
- 'correctAnswer': The right word that **best fits the context**.

---

### **✅ Rules for Generating Questions & Options**
✔ **Make the context match the word's actual meaning.**  
✔ **Ensure the correct word is the best possible fit.**  
✔ **Make the incorrect choices (distractors):**  
   - **Words that could almost fit but are slightly incorrect.**  
   - **Words that people might mistakenly associate with the sentence.**  
   - **Words that are completely out of place but funny.**  
✔ **Avoid repeating distractors across different questions.**  

---

### **📌 Example Input:**
[
  {{ ""id"": 407, ""wordText"": ""hot"", ""englishMeaning"": ""having a high degree of heat or a high temperature."" }},
  {{ ""id"": 408, ""wordText"": ""espresso"", ""englishMeaning"": ""coffee brewed by forcing hot water through finely ground coffee beans."" }},
  {{ ""id"": 409, ""wordText"": ""exhausted"", ""englishMeaning"": ""extremely tired or worn out."" }}
]

---

### **🔥 Example Output:**
[
  {{
    ""wordId"": 407,
    ""question"": ""The sun was shining directly on the black car, making its surface so ___ that you could almost fry an egg on it."",
    ""options"": [
      ""hot"",
      ""cold"",
      ""soft"",
      ""bright""
    ],
    ""correctAnswer"": ""hot""
  }},
  {{
    ""wordId"": 408,
    ""question"": ""After pulling an all-nighter for her final exam, Sarah walked into the café and ordered a double shot of ___. She needed all the energy she could get."",
    ""options"": [
      ""espresso"",
      ""orange juice"",
      ""lemonade"",
      ""cereal""
    ],
    ""correctAnswer"": ""espresso""
  }},
  {{
    ""wordId"": 409,
    ""question"": ""After running a marathon, Jake collapsed on the grass and said, 'I don’t think I can move another inch—I’m completely ___.'"",
    ""options"": [
      ""exhausted"",
      ""excited"",
      ""bored"",
      ""relaxed""
    ],
    ""correctAnswer"": ""exhausted""
  }}
]

---

🔥 **Now, generate quizzes for these words, making sure the missing word accurately represents its English meaning in a fun, engaging, and real-world context:**  

{JsonSerializer.Serialize(request.Words, new JsonSerializerOptions { WriteIndented = true })}
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

                _logger.LogInformation("Sending request to Gemini API with words: {Words}", string.Join(", ", request.Words.Select(w => w.WordText)));

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


        private List<QuizItem> ExtractJsonFromResponse(string responseString)
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

                    // 🔹 Remove Markdown formatting and trim
                    contentText = contentText.Replace("```json", "").Replace("```", "").Trim();

                    _logger.LogInformation("Extracted JSON: {ExtractedJson}", contentText);

                    // 🔹 Validate JSON format before deserialization
                    if (string.IsNullOrWhiteSpace(contentText) || !contentText.StartsWith("["))
                    {
                        _logger.LogError("Invalid JSON format: {Json}", contentText);
                        return new List<QuizItem>
                {
                    new QuizItem
                    {
                        WordId = -1,
                        Question = "Invalid response format.",
                        Options = new List<string> { "N/A", "N/A", "N/A", "N/A" },
                        CorrectAnswer = "N/A"
                    }
                };
                    }

                    // 🔹 Deserialize JSON into List<QuizItem>
                    return JsonSerializer.Deserialize<List<QuizItem>>(contentText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<QuizItem>();
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError("JSON deserialization error: {Message}", jsonEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error extracting JSON: {Message}", ex.ToString());
            }

            return new List<QuizItem>
    {
        new QuizItem
        {
            WordId = -1,
            Question = "Error parsing quiz data.",
            Options = new List<string> { "N/A", "N/A", "N/A", "N/A" },
            CorrectAnswer = "N/A"
        }
    };
        }

    }

    public class GenerateQuizRequest
    {
        public List<WordQuizItem> Words { get; set; }
    }

    public class WordQuizItem
    {
        public int Id { get; set; }
        public string WordText { get; set; }
        public string EnglishMeaning { get; set; }
    }

    public class QuizItem
    {
        [JsonPropertyName("wordId")]
        public int WordId { get; set; }

        [JsonPropertyName("question")]
        public string Question { get; set; }

        [JsonPropertyName("options")]
        public List<string> Options { get; set; }

        [JsonPropertyName("correctAnswer")]
        public string CorrectAnswer { get; set; }
    }

}