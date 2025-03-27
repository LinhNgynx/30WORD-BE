using GeminiTest.Data;
using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
