using GeminiTest.Data;
using GeminiTest.DTO;
using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace GeminiTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WordlistController : ControllerBase
    {
        private readonly DataContext _context;

        public WordlistController(DataContext context)
        {
            _context = context;
        }

        // ✅ Save a wordlist with words
        [HttpPost("SaveWordlist")]
        [Authorize]
        public async Task<IActionResult> SaveWordlist([FromBody] List<WordDto> words)
        {
            try
            {
                
                if (words == null || words.Count == 0)
                {
                    return BadRequest(new { message = "Wordlist cannot be empty" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var wordlist = new Wordlist
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Name = "Wordlist Default Name",
                    Description = "Wordlist Default Description.",
                    Words = words.Select(w => new Word
                    {
                        WordText = w.Word,
                        Phonetic = w.Phonetic,
                        PartOfSpeech = w.PartOfSpeech,
                        EnglishMeaning = w.EnglishMeaning,
                        VietnameseMeaning = w.VietnameseMeaning,
                        ExampleSentence = w.Example
                    }).ToList()
                };

                _context.Wordlists.Add(wordlist);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Wordlist saved successfully!",
                    wordlistId = wordlist.Id,
                    name = wordlist.Name,
                    description = wordlist.Description,
                    wordCount = wordlist.Words.Count
                });
            }
            catch (Exception ex)
            {
                // Log the error (consider using ILogger for better logging)
                Console.WriteLine($"Error saving wordlist: {ex.Message}");

                return StatusCode(500, new { message = "An error occurred while saving the wordlist.", error = ex.Message });
            }
        }


        // ✅ Fetch wordlists of user by date
        [HttpGet("GetWordlistsByDate")]
        [Authorize]
        public async Task<IActionResult> GetWordlistsByDate([FromQuery] DateTime date)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var wordlists = await _context.Wordlists
                    .Where(wl => wl.UserId == userId && wl.CreatedAt.Date == date.Date)
                    .Select(wl => new
                    {
                        wordlistId = wl.Id,
                        name = wl.Name,
                        description = wl.Description,
                        createdAt = wl.CreatedAt,
                        words = wl.Words.Select(w => new
                        {
                            w.Id,
                            w.WordText,
                            w.Phonetic,
                            w.PartOfSpeech,
                            w.EnglishMeaning,
                            w.VietnameseMeaning,
                            w.ExampleSentence
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(wordlists);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching wordlists.", error = ex.Message });
            }
        }

        [HttpPut("EditWordlist")]
        [Authorize]
        public async Task<IActionResult> EditWordlist([FromBody] EditWordlistDto wordlistDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var wordlist = await _context.Wordlists
                    .FirstOrDefaultAsync(wl => wl.Id == wordlistDto.WordlistId && wl.UserId == userId);

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }

                // Update fields
                wordlist.Name = wordlistDto.Name;
                wordlist.Description = wordlistDto.Description;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Wordlist updated successfully!",
                    wordlistId = wordlist.Id,
                    name = wordlist.Name,
                    description = wordlist.Description
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the wordlist.", error = ex.Message });
            }
        }

        [HttpDelete("DeleteWordlist/{wordlistId}")]
        [Authorize]
        public async Task<IActionResult> DeleteWordlist(int wordlistId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var wordlist = await _context.Wordlists
                    .Include(wl => wl.Words) // Ensure words are also deleted
                    .FirstOrDefaultAsync(wl => wl.Id == wordlistId && wl.UserId == userId);

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }

                _context.Wordlists.Remove(wordlist);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Wordlist deleted successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the wordlist.", error = ex.Message });
            }
        }


    }
}
