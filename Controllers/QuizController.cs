using GeminiTest.Data;
using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<QuizController> _logger;
        public QuizController(DataContext context, ILogger<QuizController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("get/{quizType}/{wordListId}")]
        [Authorize]
        public async Task<IActionResult> GetQuizzesByTypeAndWordList(string quizType, int wordListId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Convert quizType string to QuizType enum
                if (!Enum.TryParse<QuizType>(quizType, true, out var quizTypeEnum))
                {
                    return BadRequest(new { message = "Invalid quiz type." });
                }

                var quizzes = await _context.Quizzes
                    .Include(q => q.Word)
                    .Where(q => q.Type == (QuizType)quizTypeEnum && q.Word.WordlistId == wordListId)
                    .ToListAsync();

                if (quizzes == null || quizzes.Count == 0)
                {
                    return NotFound(new { message = $"No quizzes found for wordlist {wordListId} and type '{quizType}'." });
                }

                var result = quizzes.Select(q => new
                {
                    q.Id,
                    q.WordId,
                    q.Type,
                    q.Question,
                    Options = JsonConvert.DeserializeObject<List<string>>(q.OptionsJson),
                    q.CorrectAnswer,
                    q.CreatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching quizzes: {ex.Message}");
            }
        }



        [HttpPost("save/{quizType}")]
        [Authorize]
        public async Task<IActionResult> SaveQuizzes([FromRoute] string quizType, [FromBody] List<GeneratedQuiz> quizzes)
        {
            try
            {
                if (quizzes == null || quizzes.Count == 0)
                {
                    return BadRequest(new { message = "No quizzes provided in request." });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated." });
                }

                if (!Enum.TryParse<QuizType>(quizType, true, out var parsedQuizType))
                {
                    return BadRequest(new { message = "Invalid quiz type provided." });
                }

                var quizEntities = quizzes.Select(q => new Quiz
                {
                    WordId = q.WordId,
                    Type = parsedQuizType, // No need for explicit casting
                    Question = q.Question,
                    OptionsJson = JsonConvert.SerializeObject(q.Options),
                    CorrectAnswer = q.CorrectAnswer,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                // Use transaction for reliability
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.Quizzes.AddRangeAsync(quizEntities);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed while saving quizzes.");
                    return StatusCode(500, new { message = "Error saving quizzes. Please try again later." });
                }

                return Ok(new { message = $"Quizzes of type '{quizType}' saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save quizzes.");
                return StatusCode(500, new { message = "Error saving quizzes. Please try again later." });
            }
        }

        [HttpPost("submit")]
        [Authorize]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmitDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated." });
            }

            var wordlist = await _context.Wordlists
                .FirstOrDefaultAsync(w => w.Id == dto.WordlistId && w.UserId == userId);

            if (wordlist == null)
            {
                return NotFound(new { message = "Wordlist not found or does not belong to the user." });
            }

            bool progressUpdated = false;
            bool highestScoreUpdated = false;
            int currentHighestScore = 0;
            int progressThreshold = 0;

            // ✅ Convert QuizType string to Enum
            if (!Enum.TryParse<QuizType>(dto.QuizType, true, out QuizType quizType))
            {
                _logger.LogError($"Invalid QuizType received: {dto.QuizType}");
                return BadRequest(new { message = $"Invalid quiz type: {dto.QuizType}. Allowed values: {string.Join(", ", Enum.GetNames(typeof(QuizType)))}" });
            }



            // ✅ Save the latest score (always)
            if (quizType == QuizType.Meaning)
            {
                wordlist.LatestMeaningScore = dto.Score;
                if (dto.Score > wordlist.HighestMeaningScore)
                {
                    wordlist.HighestMeaningScore = dto.Score;
                    highestScoreUpdated = true;
                }
                currentHighestScore = wordlist.HighestMeaningScore;
                progressThreshold = 1;
            }
            else if (quizType == QuizType.ContextUsage)
            {
                wordlist.LatestContextScore = dto.Score;
                if (dto.Score > wordlist.HighestContextScore)
                {
                    wordlist.HighestContextScore = dto.Score;
                    highestScoreUpdated = true;
                }
                currentHighestScore = wordlist.HighestContextScore;
                progressThreshold = 2;
            }

            // ✅ Update progress if highest score exceeds 80 and matches the threshold
            if (highestScoreUpdated && currentHighestScore > 80 && wordlist.Progress == progressThreshold)
            {
                wordlist.Progress++;
                progressUpdated = true;
            }
            // 🔥 ✅ Optimized Batch Fetch for Words Instead of Multiple FindAsync Calls
            var wordIds = dto.Answers.Select(a => a.WordId).ToList();
            var words = await _context.Words.Where(w => wordIds.Contains(w.Id)).ToListAsync();

            foreach (var answer in dto.Answers)
            {
                var word = words.FirstOrDefault(w => w.Id == answer.WordId);
                if (word != null)
                {
                    UpdateSpacedRepetition(word, answer.IsCorrect);
                }
            }

            // ✅ Single Database Save Instead of Multiple Calls
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Quiz score updated successfully",
                progressUpdated,
                newProgress = wordlist.Progress
            });
        }

        [HttpGet("quiz-history")]
        [Authorize]
        public async Task<IActionResult> GetUserQuizHistory()
        {
            try
            {
                // Retrieve user ID from the claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated." });
                }

                // Retrieve the quiz history for the user
                var userQuizzes = await _context.Wordlists
                    .Where(wl => wl.UserId == userId)  // Filter wordlists by userId
                    .SelectMany(wl => wl.Words)        // Get all words from those wordlists
                    .SelectMany(w => w.Quizzes)        // Get all quizzes related to those words
                    .GroupBy(q => q.CreatedAt.Date)    // Group quizzes by creation date
                    .Select(g => new { date = g.Key, count = g.Count() })  // Project the count per date
                    .ToListAsync();  // Retrieve as a list

                // If no quizzes found, return an empty array with a message
                if (userQuizzes == null || userQuizzes.Count == 0)
                {
                    return Ok(new { message = "No quizzes found for this user.", data = new List<object>() });
                }

                // Return the quiz history
                return Ok(userQuizzes);
            }
            catch (Exception ex)
            {
                // Log the error (can be logged to a file or a monitoring service)
                _logger.LogError(ex, "An error occurred while fetching the quiz history.");

                // Return a generic error message to the client
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }



        private void UpdateSpacedRepetition(Word word, bool isCorrect)
        {
            DateTime today = DateTime.UtcNow.Date;

            // ✅ Allow first-time updates regardless of date
            if (word.LastReviewDate == DateTime.MinValue || word.LastReviewDate.Date != today)
            {
                if (isCorrect)
                {
                    word.CorrectStreak++;
                }
                else
                {
                    word.CorrectStreak = 0;
                }

                int[] reviewIntervals = { 1, 3, 7, 14, 30 };
                int index = Math.Min(word.CorrectStreak, reviewIntervals.Length - 1);
                word.NextReviewDate = today.AddDays(reviewIntervals[index]);
                word.LastReviewDate = today;
                if (word.CorrectStreak >= 4) word.FluencyValue = (int)FluencyLevel.Mastered;
                else if (word.CorrectStreak >= 3) word.FluencyValue = (int)FluencyLevel.Advanced;
                else if (word.CorrectStreak >= 2) word.FluencyValue = (int)FluencyLevel.Proficient;
                else if (word.CorrectStreak >= 1) word.FluencyValue = (int)FluencyLevel.Familiar;
                else word.FluencyValue = (int)FluencyLevel.Beginner;
            }
        }



    }
    public class QuizSubmitDto
    {
        [Required]
        public int WordlistId { get; set; }

        [Required]
        public string QuizType { get; set; }  // ✅ Use Enum instead of string

        [Range(0, 100, ErrorMessage = "Score must be between 0 and 100.")]
        public int Score { get; set; }  // ✅ Ensure valid score

        [Required]
        public List<AnswerDto> Answers { get; set; } = new List<AnswerDto>(); // ✅ Add answers for spaced repetition
    }

    public class AnswerDto
    {
        [Required]
        public int WordId { get; set; }  // ✅ Reference to the word in the quiz

        [Required]
        public bool IsCorrect { get; set; }  // ✅ Indicates if the answer was correct
    }



    // Model matching API request format
    public class GeneratedQuiz
    {
        public int WordId { get; set; }
        public string Question { get; set; }
        public List<string> Options { get; set; }
        public string CorrectAnswer { get; set; }
    }
}
