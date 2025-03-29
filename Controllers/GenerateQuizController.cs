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
        [HttpPost("generate/Meaning")]
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
- 'question': A clear, engaging multiple-choice question about the word’s meaning, with an icon appearing immediately after the question mark.
- 'options': A list of four possible answers, including the correct one.
- 'correctAnswer': The correct answer.

---

### **✅ Rules for Generating Options:**
✔ The **correct meaning** should always be included.  
✔ The **three distractors** must:  
  - Be plausible but **incorrect interpretations** of the word.  
  - **Have similar meaning but still be clearly incorrect.**  
  - **Match the length and structure** of the correct answer.  
  - Avoid repeating choices across different questions.  
✔ Do **NOT** make distractors too confusing—users should hesitate but **not be able to argue they are the same as the correct answer**.

---

### **📌 Rules for Icons:**
✔ Use a **relevant emoji** or small image URL inline within the question.  
✔ The icon **must appear immediately** after the question mark.  
✔ If no relevant emoji exists, return an empty space (`""`).  

---

### **🔥 Example Input:**
[
  {{ ""id"": 407, ""wordText"": ""meticulous"", ""englishMeaning"": ""showing great attention to detail; very careful and precise."" }},
  {{ ""id"": 408, ""wordText"": ""fortuitous"", ""englishMeaning"": ""happening by chance, especially in a lucky way."" }},
]

---

### **🎯 Example Output (with well-balanced options):**
[
  {{
    ""wordId"": 407,
    ""question"": ""What does the word 'meticulous' mean? 🔍"",
    ""options"": [
      ""Showing great attention to detail and precision."",
      ""Being careful but sometimes missing small details."",
      ""Having a strong passion for organization and structure."",
      ""Following rules strictly without allowing flexibility.""
    ],
    ""correctAnswer"": ""Showing great attention to detail and precision.""
  }},
  {{
    ""wordId"": 408,
    ""question"": ""Which of the following best defines 'fortuitous'? 🍀"",
    ""options"": [
      ""Happening by chance in a lucky or fortunate way."",
      ""Being well-prepared for all possible outcomes."",
      ""Occurring randomly without any beneficial effect."",
      ""Having a strong instinct for making good decisions.""
    ],
    ""correctAnswer"": ""Happening by chance in a lucky or fortunate way.""
  }}
]

---

🔥 **Now, generate quizzes for the following words, ensuring that incorrect choices have a similar length and structure, while remaining plausible but clearly incorrect:**  

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

        [HttpPost("generate/ContextUsage")]
        public async Task<IActionResult> GenerateContext([FromBody] GenerateQuizRequest request)
        {
            if (request.Words == null || request.Words.Count == 0)
            {
                return BadRequest(new { error = "No words provided." });
            }

            string prompt = $@"
You're designing a **fun and educational quiz** that helps users learn words by challenging them to identify **the correct contextual usage**.  
Each question should be structured as follows:

- 'wordId': The original ID of the word.
- 'question': A sentence **asking which usage of the word is correct, with an icon appearing immediately after the question mark**.
- 'options': Four **different sentences using the word**, but only one is correct,  with an icon appearing immediately after the option.
- 'correctAnswer': The **correct sentence** that naturally and accurately uses the word but still engaging, funny and memorable.

---

### **✅ Rules for Generating Questions & Options**
✔ **The correct sentence must use the word in a natural and meaningful way.**  
✔ **Incorrect sentences (distractors) should:**  
   - **Sound logical but have slight misuses.**  
   - **Include common misconceptions about the word.**  
   - **Be completely incorrect but still sound plausible, engaging, funny to help user feel comfortable.**  
✔ **All answer choices should be similar in length and structure** to prevent users from picking the longest or shortest option.  
✔ **Avoid repeating distractors across different questions.**  

---

### **📌 Rules for Icons:**
✔ Use a **relevant emoji** inline within the question.  
✔ The icon **must appear immediately** after the question mark.  
✔ Each answer choice **must have an emoji at the end**, related to its context.  
✔ If no relevant emoji exists, return an empty space (`""`).  


---

### **🔥 Example Input:**
[
  {{ ""id"": 407, ""wordText"": ""meticulous"", ""englishMeaning"": ""showing great attention to detail; very careful and precise."" }},
  {{ ""id"": 408, ""wordText"": ""fortuitous"", ""englishMeaning"": ""happening by chance, especially in a lucky way."" }}
]

---

### **🎯 Example Output (Funny & Engaging with Icons):**
[
  {{
    ""wordId"": 407,
    ""question"": ""Which sentence correctly uses the word 'meticulous'? 🧐"",
    ""options"": [
      ""She was meticulous, organizing her sock drawer by color, size, and sock personality. 🧦"",   
      ""He was meticulous about eating and inhaled his burger in two bites. 🍔"",  
      ""She was meticulous in her strategy to randomly throw clothes in her suitcase. 🎒"",  
      ""He was meticulous, making sure to always leave a mess behind. 💥""
    ],
    ""correctAnswer"": ""She was meticulous, organizing her sock drawer by color, size, and sock personality. 🧦""
  }},
  {{
    ""wordId"": 408,
    ""question"": ""Which sentence correctly uses the word 'fortuitous'? 🍀"",
    ""options"": [
      ""It was fortuitous that she found a $20 bill on the ground as she was craving pizza. 🍕"",  
      ""He was fortuitous and predicted the winning lottery numbers using science. 🔮"",  
      ""The chef was fortuitous, blindly adding salt instead of sugar. 🧂"",  
      ""His fortuitous speech was 100% scripted and rehearsed for weeks. 🎤""
    ],
    ""correctAnswer"": ""It was fortuitous that she found a $20 bill on the ground as she was craving pizza. 🍕""
  }}
]


---

🔥 **Now, generate quizzes for these words, ensuring that all answer choices have a similar length and structure while keeping the incorrect ones subtly misleading. The correct answer should match the word’s meaning precisely, while the distractors should follow the rules of plausible but incorrect usage.**  

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