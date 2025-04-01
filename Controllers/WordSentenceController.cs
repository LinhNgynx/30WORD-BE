using GeminiTest.Data;
using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Claims;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WordSentenceController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<WordSentenceController> _logger;
        public WordSentenceController(DataContext context, ILogger<WordSentenceController> logger)
        {
            _context = context;
            _logger = logger;
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
                        Point = 0,
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
                    ws.SentenceText,
                    ws.Feedback,
                    ws.Point,
                    ws.CreatedDate,
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching WordSentences: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }


    }
}
