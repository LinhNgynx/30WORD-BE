﻿using GeminiTest.Data;
using GeminiTest.DTO;
using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;
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
                        Progress = wl.Progress,
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
        [HttpGet("GetWordlistById/{wordlistId}")]
        [Authorize]
        public async Task<IActionResult> GetWordlistById(int wordlistId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var wordlist = await _context.Wordlists
                    .Where(wl => wl.Id == wordlistId && wl.UserId == userId)
                    .Select(wl => new
                    {
                        wordlistId = wl.Id,
                        name = wl.Name,
                        description = wl.Description,
                        createdAt = wl.CreatedAt,
                        Progress = wl.Progress,
                        HighestMeaningScore = wl.HighestMeaningScore,
                        LatestMeaningScore = wl.LatestMeaningScore,
                        HighestContextScore = wl.HighestContextScore,
                        LatestContextScore = wl.LatestContextScore,
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
                    .FirstOrDefaultAsync();

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }

                return Ok(wordlist);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the wordlist.", error = ex.Message });
            }
        }
        [HttpDelete("{wordlistId}/words/{wordId}")]
        [Authorize]
        public async Task<IActionResult> DeleteWord(int wordlistId, int wordId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Fetch the wordlist with the matching user ID
                var wordlist = await _context.Wordlists
                    .Include(wl => wl.Words)
                    .FirstOrDefaultAsync(wl => wl.Id == wordlistId && wl.UserId == userId);

                if (wordlist == null)
                {
                    return NotFound(new { message = "Wordlist not found or does not belong to the user." });
                }

                // Find the specific word to delete
                var word = wordlist.Words.FirstOrDefault(w => w.Id == wordId);
                if (word == null)
                {
                    return NotFound(new { message = "Word not found in the wordlist." });
                }

                // Remove the word from the list
                wordlist.Words.Remove(word);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Word deleted successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the word.", error = ex.Message });
            }
        }
        [HttpGet("review-today")]
        [Authorize]
        public async Task<IActionResult> GetWordsToReviewToday([FromQuery] int take = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var today = DateTime.UtcNow.Date;

                var reviewWords = await _context.Words
                    .Where(w => w.Wordlist.UserId == userId && w.NextReviewDate <= today)
                    .OrderBy(w => w.NextReviewDate)
                    .Take(take)
                    .Select(w => new
                    {
                        w.Id,
                        w.WordText,
                        w.NextReviewDate,
                        w.FluencyValue,
                        w.EnglishMeaning,
                        w.VietnameseMeaning,
                        w.PartOfSpeech,
                        w.ExampleSentence
                    })
                    .ToListAsync();

                return Ok(reviewWords);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching words to review.", error = ex.Message });
            }
        }

        
        [HttpPost("update-review-status")]
        [Authorize]
        public async Task<IActionResult> UpdateReviewStatus([FromBody] ReviewUpdateDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            var word = await _context.Words
                .FirstOrDefaultAsync(w => w.Id == dto.WordId &&  w.Wordlist.UserId == userId);

            if (word == null)
                return NotFound(new { message = "Word not found" });

            if (dto.SkipReview)
            {
                word.NextReviewDate = DateTime.MaxValue;
                word.LastReviewDate = DateTime.UtcNow.Date;
            }
            else
            {
                UpdateSpacedRepetition(word, dto.IsCorrect);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("user-word-summary")]
        [Authorize]
        public async Task<IActionResult> GetUserWordSummary()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var words = await _context.Words
                .Where(w => w.Wordlist.UserId == userId)
                .ToListAsync();

            var summary = new WordSummaryDto
            {
                TotalWords = words.Count,
                BeginnerCount = words.Count(w => w.FluencyValue == (int)FluencyLevel.Beginner),
                FamiliarCount = words.Count(w => w.FluencyValue == (int)FluencyLevel.Familiar),
                ProficientCount = words.Count(w => w.FluencyValue == (int)FluencyLevel.Proficient),
                AdvancedCount = words.Count(w => w.FluencyValue == (int)FluencyLevel.Advanced),
                MasteredCount = words.Count(w => w.FluencyValue == (int)FluencyLevel.Mastered)
            };

            return Ok(summary);
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
    public class WordSummaryDto
    {
        public int TotalWords { get; set; }
        public int BeginnerCount { get; set; }
        public int FamiliarCount { get; set; }
        public int ProficientCount { get; set; }
        public int AdvancedCount { get; set; }
        public int MasteredCount { get; set; }
    }
    public class ReviewUpdateDto
    {
        public int WordId { get; set; }
        public bool IsCorrect { get; set; }
        public bool SkipReview { get; set; }
    }


}
