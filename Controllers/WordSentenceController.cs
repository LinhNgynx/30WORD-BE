using GeminiTest.Data;
using GeminiTest.DTO;
using GeminiTest.Models;
using GeminiTest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
    public class WordSentenceController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<WordSentenceController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;
        private readonly IPromptService _promptService;
        public WordSentenceController(IHttpClientFactory httpClientFactory, IOptions<GeminiSettings> geminiSettings, DataContext context, ILogger<WordSentenceController> logger, IPromptService promptService)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = geminiSettings.Value.ApiKeySentence;
            _context = context;
            _logger = logger;
            _promptService = promptService; // Injecting PromptService
        }

        [HttpPost("createSentence")]
        [Authorize]
        public async Task<IActionResult> CreateSentence([FromBody] List<int> wordIds)
        {
            try
            {
                if (wordIds == null || !wordIds.Any())
                {
                    return BadRequest(new { message = "No Word IDs provided." });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var words = await _context.Words.Where(w => wordIds.Contains(w.Id)).ToListAsync();
                if (words.Count != wordIds.Count)
                {
                    return NotFound(new { message = "One or more Word IDs not found." });
                }

                _logger.LogInformation($"User {userId} is creating sentences for words with IDs: {string.Join(", ", wordIds)}");

                // Check if the Word already has a WordSentence
                var existingSentences = await _context.WordSentences
                    .Where(ws => wordIds.Contains(ws.WordId))
                    .ToListAsync();

                // Create only the WordSentences that do not already exist
                var wordSentencesToAdd = words
                    .Where(word => !existingSentences.Any(ws => ws.WordId == word.Id))
                    .Select(word => new WordSentence
                    {
                        WordId = word.Id,
                        SentenceText = "",
                        Feedback = "",
                        CreatedDate = DateTime.UtcNow
                    })
                    .ToList();

                if (!wordSentencesToAdd.Any())
                {
                    return BadRequest(new { message = "All WordSentences already exist." });
                }

                // **Use transaction for reliability**
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.WordSentences.AddRange(wordSentencesToAdd);
                    await _context.SaveChangesAsync();

                    // Commit the transaction after successful save
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Transaction failed while creating sentences: {ex}");
                    return StatusCode(500, new { message = "Error creating sentences. Please try again later." });
                }

                return Ok(new { message = "Sentences created successfully.", data = wordSentencesToAdd });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in creating sentences: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("getByWordlist/{wordlistId}")]
        [Authorize]
        public async Task<IActionResult> GetWordSentencesByWordlistId(int wordlistId)
        {
            try
            {

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }
                var wordlist = await _context.Wordlists
                    .FirstOrDefaultAsync(w => w.Id == wordlistId && w.UserId == userId);

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }
                // Fetch all WordSentences that belong to the Wordlist (assuming WordSentence has a WordlistId)
                var wordSentences = await _context.WordSentences
                  .Include(ws => ws.Word)  // Include related Word entity
                  .Where(ws => ws.Word.WordlistId == wordlistId) // Filter based on WordlistId
                  .ToListAsync();


                if (!wordSentences.Any())
                {
                    return NotFound(new { message = "No WordSentences found for this Wordlist." });
                }
                var result = wordSentences.Select(ws => new
                {
                    ws.Id,
                    ws.WordId,
                    WordText = ws.Word.WordText,
                    Meaning = ws.Word.EnglishMeaning,
                    ws.SentenceText,
                    ws.Feedback,
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching WordSentences: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpPut("save-answer")]
        [Authorize]

        public async Task<IActionResult> UpdateSentence([FromBody] SentencePayLoad sentencePayLoad)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sentencePayLoad.SentenceText) ||
                  string.IsNullOrWhiteSpace(sentencePayLoad.Feedback))
                {
                    return BadRequest("Invalid sentence or feedback.");
                }
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }
                var wordlist = await _context.Wordlists
                    .FirstOrDefaultAsync(wl => wl.Id == sentencePayLoad.WordlistId && wl.UserId == userId);

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }
                var wordSentence = await _context.WordSentences.FindAsync(sentencePayLoad.Id);

                wordSentence.Feedback = sentencePayLoad.Feedback;
                wordSentence.SentenceText = sentencePayLoad.SentenceText;

                _context.WordSentences.Update(wordSentence);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Answer saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving sentences: {ex.Message}");
            }
        }
        [HttpPost("evaluate-sentence")]
        [Authorize]
        public async Task<IActionResult> EvaluateSentence([FromBody] SentenceEvaluateRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated." });
            }
            if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Sentence))
            {
                return BadRequest("Word and sentence are required.");
            }
            var user = await _context.Users.FindAsync(userId);
            var favoriteDog = user.FavoriteDog.ToString().ToLower() ?? "pomeranian";
            string prompt = _promptService.GetPromptByDog(favoriteDog, request.Word, request.Sentence, request.Meaning);


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

                _logger.LogInformation("Sending request to Gemini API to evaluate word");

                var response = await httpClient.PostAsync(apiUrl, jsonContent);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = "Error calling Gemini API.", details = errorMessage });
                }

                // Get the response content as string
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse the response content (assuming it's JSON)
                _logger.LogInformation("Raw Gemini API Evaluate:", responseContent);

                var extractedJson = ExtractJsonFromResponse(responseContent);

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
        

        private FeedbackResponse ExtractJsonFromResponse(string responseString)
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

                    contentText = contentText.Replace("```json", "").Replace("```", "").Trim();

                    _logger.LogInformation("Extracted JSON: {ExtractedJson}", contentText);

                    var response = JsonSerializer.Deserialize<FeedbackResponse>(contentText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // If deserialization fails (content is not valid JSON), return null
                    return response ?? new FeedbackResponse();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error extracting JSON from Gemini response: {Message}", ex.ToString());
            }
            return new FeedbackResponse();

        }
    }
        public class SentenceEvaluateRequest
    {
        public string Word { get; set; }
        public string Meaning { get; set; }       // Now matches frontend payload
        public string Sentence { get; set; }
    }
    public class SentencePayLoad
    {
        public int WordlistId { get; set; }
        public int Id { get; set; } // Sentence ID (0 or null if it's a new sentence)
        public string SentenceText { get; set; }
        public string Feedback { get; set; }
    }

    public class FeedbackResponse
    {
        [JsonPropertyName("feedback")]
        public string Feedback { get; set; }

        [JsonPropertyName("animation")]
        public string Animation { get; set; }
    }


}
