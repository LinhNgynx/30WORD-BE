﻿using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeminiTest.Setting;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenerateQuizController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string _apiKey;
        private readonly ILogger<GenerateQuizController> _logger;

        public GenerateQuizController(UserManager<ApplicationUser> userManager, IHttpClientFactory httpClientFactory, IOptions<GeminiSettings> geminiSettings, ILogger<GenerateQuizController> logger)
        {
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _apiKey = geminiSettings.Value.ApiKeyQuiz;
            _logger = logger;
        }
        [HttpPost("generate/Meaning")]
        [Authorize]
        public async Task<IActionResult> GenerateContent([FromBody] GenerateQuizRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });
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
        [Authorize]
        public async Task<IActionResult> GenerateContext([FromBody] GenerateQuizRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });
            if (request.Words == null || request.Words.Count == 0)
            {
                return BadRequest(new { error = "No words provided." });
            }
            int minWords = 8;
            int maxWords = 10;

            switch (user.Level)
            {
                case UserLevel.Beginner:
                    minWords = 8;
                    maxWords = 10;
                    break;
                case UserLevel.Intermediate:
                    minWords = 10;
                    maxWords = 13;
                    break;
                case UserLevel.Advanced:
                    minWords = 13;
                    maxWords = 18;
                    break;
            }
            string exampleOutput = user.Level switch
            {
                UserLevel.Beginner => @"
### **🎯 Example Output (Beginner - 8-10 words):**
[
  {
    ""wordId"": 407,
    ""question"": ""Which sentence correctly uses the word 'meticulous'? 🧐"",
    ""options"": [
      ""She was meticulous, folding each shirt neatly. 👕"",
      ""He was meticulous and skipped planning every day. 📅"",
      ""She was meticulous, throwing socks around the room. 🧦"",
      ""He was meticulous, but forgot to set alarms. ⏰""
    ],
    ""correctAnswer"": ""She was meticulous, folding each shirt neatly. 👕""
  }
]",
                UserLevel.Intermediate => @"
### **🎯 Example Output (Intermediate - 10-13 words):**
[
  {
    ""wordId"": 407,
    ""question"": ""Which sentence correctly uses the word 'meticulous'? 🧐"",
    ""options"": [
      ""She was meticulous, sorting pens by type, size, and color. 🖊️"",
      ""He was meticulous, losing notes and never organizing them. 📝"",
      ""She was meticulous, piling clothes without looking. 🧥"",
      ""He was meticulous, ignoring all tiny details in the project. 🔍""
    ],
    ""correctAnswer"": ""She was meticulous, sorting pens by type, size, and color. 🖊️""
  }
]",
                UserLevel.Advanced => @"
### **🎯 Example Output (Advanced - 13-18 words):**
[
  {
    ""wordId"": 407,
    ""question"": ""Which sentence correctly uses the word 'meticulous'? 🧐"",
    ""options"": [
      ""She was meticulous, reviewing every sentence in her essay for grammar and clarity. 📄"",
      ""He was meticulous, randomly selecting items without any attention to detail. 🎯"",
      ""She was meticulous, ignoring deadlines and missing obvious mistakes in reports. 📊"",
      ""He was meticulous, throwing all tools into a single messy box. 🧰""
    ],
    ""correctAnswer"": ""She was meticulous, reviewing every sentence in her essay for grammar and clarity. 📄""
  }
]",
                _ => ""
            };

            string prompt = $@"
### **📚 Word Usage Quiz Generator**  
You're creating a **fun yet educational quiz** to help users master word usage through multiple-choice questions.  

Each question should be structured as follows:  

- **wordId**: The unique ID of the word.  
- **question**: A sentence **asking which usage of the word is correct**, with an **emoji appearing immediately after the question mark**.  
- **options**: **Four sentences using the word**, but **only one is correct**.  
- **correctAnswer**: The precise, naturally correct sentence that fits the word's meaning.  

---

### **✅ Rules for Generating Questions & Options**  

✔ **Correct Usage (1 Answer):**  
   - The correct sentence must be **precise, natural, and NOT exaggerated or funny**.  
   - It should **clearly convey the word's meaning** in a professional yet engaging way.  
   - **Must be {minWords}-{maxWords} words long** for consistency.  

✔ **Incorrect Usage (3 Distractors):**  
   - **Each incorrect option must misuse the word in a common but plausible way.**  
   - **Can be humorous but should sound like something a learner might mistakenly believe.**  
   - **Each distractor should follow a different type of mistake:**
     - A **slightly wrong meaning** that seems close but is incorrect.
     - A **completely incorrect use** that still sounds grammatically correct.
     - A **misunderstanding of the word’s function** (e.g., using a noun as a verb).  
   - **Must be {minWords}-{maxWords} words long** (same length as the correct answer).  
   - **Avoid repeating distractors across different questions.**  

✔ **Formatting Rules:**  
   - The **emoji must appear immediately after the question mark** in the question.  
   - Each answer choice **must end with an emoji** related to its context.  
   - If no relevant emoji exists, return an empty space (`""`).  

---
---

### **🔥 Example Input:**
[
  {{ ""id"": 407, ""wordText"": ""meticulous"", ""englishMeaning"": ""showing great attention to detail; very careful and precise."" }},
]

---

{exampleOutput}

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