using GeminiTest.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using GeminiTest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

[Route("api/account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccountController> _logger;


    public AccountController(UserManager<ApplicationUser> userManager, IEmailSender emailSender, IMemoryCache cache, ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            fullName = user.FullName,
            favoriteDog = user.FavoriteDog.ToString(), // convert enum to string
            level = user.Level.ToString()
        });
    }

    [HttpPost("forgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
    {
        _logger.LogInformation("🔍 Received forgot password request for {Email}", model.Email);

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            _logger.LogWarning("⚠️ Forgot password request received with an empty email.");
            return BadRequest("Email is required.");
        }

        var normalizedEmail = model.Email.Trim().ToLower();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user == null)
        {
            _logger.LogWarning("⚠️ Email {Email} not found in the database", model.Email);
            return BadRequest("Recheck your email"); // Prevents email enumeration
        }

        var otp = GenerateOtp();
        var cacheKey = $"RESET_OTP_{normalizedEmail}";
        _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(5));

        _logger.LogInformation("✅ OTP {Otp} generated for email {Email}", otp, model.Email);

        string emailBody = $@"
    <div style='font-family: Arial, sans-serif; padding: 10px; border: 1px solid #ddd; background: #f9f9f9;'>
        <h2 style='color: #2c3e50;'>Password Reset OTP</h2>
        <p>Your OTP is: <strong style='color: #e74c3c;'>{otp}</strong></p>
        <p>This OTP expires in <strong>5 minutes</strong>.</p>
        <p style='font-size: 12px; color: #7f8c8d;'>If you did not request this, please ignore this email.</p>
    </div>";

        try
        {
            await _emailSender.SendEmailAsync(user.Email, "Password Reset OTP", emailBody);
            _logger.LogInformation("📧 OTP email sent successfully to {Email}", model.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception occurred while sending OTP email to {Email}", model.Email);
            return StatusCode(500, "An error occurred while sending the OTP email.");
        }

        return Ok("If the email exists, an OTP has been sent.");
    }




    [HttpPost("resetPassword")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
    {
        var normalizedEmail = model.Email.ToLower();
        var cacheKey = $"RESET_OTP_{normalizedEmail}";

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null) return BadRequest("Recheck your email");
        if (!_cache.TryGetValue(cacheKey, out string storedOtp) || storedOtp != model.Otp)
            return BadRequest("Invalid or expired OTP.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        _cache.Remove(cacheKey);
        return Ok("Password has been reset successfully.");
    }

    private string GenerateOtp()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);

        int otp = Math.Abs(BitConverter.ToInt32(bytes, 0) % 1000000);

        return otp.ToString("D6"); // Ensures 6-digit formatting
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest model)
    {
        if (string.IsNullOrEmpty(model.Email))
            return BadRequest(new { message = "Email is required." });

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return BadRequest(new { message = "User not found." });

        if (user.EmailConfirmed)
            return BadRequest(new { message = "Email is already verified." });

        var otp = GenerateOtp();
        var cacheKey = $"EMAIL_OTP_{model.Email.Trim().ToLower()}";
        _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(5));

        string emailBody = $@"
        <div style='font-family: Arial, sans-serif; padding: 10px; border: 1px solid #ddd; background: #f9f9f9;'>
            <h2>Email Verification OTP</h2>
            <p>Your OTP is: <strong>{otp}</strong></p>
            <p>This OTP expires in 5 minutes.</p>
            <p>If you did not request this, please ignore this email.</p>
        </div>";

        try
        {
            await _emailSender.SendEmailAsync(user.Email, "Email Verification OTP", emailBody);
            _logger.LogInformation("OTP  sent to email {Email}", user.Email);
            return Ok(new { message = "OTP sent successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP to {Email}", user.Email);
            return StatusCode(500, new { message = "Failed to send OTP." });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null) return BadRequest(new { message = "User not found." });
        var cacheKey = $"EMAIL_OTP_{model.Email.Trim().ToLower()}";
        if (!_cache.TryGetValue(cacheKey, out string storedOtp) || storedOtp != model.Otp)
            return BadRequest(new { message = "Invalid or expired OTP." });


        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        _cache.Remove(cacheKey);
        return Ok(new { message = "Email verified successfully." });
    }
    [HttpPatch("profile")]
    [Authorize]
    public async Task<IActionResult> PatchProfile([FromBody] PatchProfileRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "User not authenticated." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { message = "User not found" });

        // Apply changes only if provided
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName;

        // Convert the FavoriteDog string to the corresponding enum (if provided)
        if (!string.IsNullOrEmpty(request.FavoriteDog))
        {
            if (Enum.TryParse<DogType>(request.FavoriteDog, true, out var dogType))
            {
                user.FavoriteDog = dogType;
            }
            else
            {
                return BadRequest(new { message = "Invalid FavoriteDog value." });
            }
        }

        // Convert the Level string to the corresponding enum (if provided)
        if (!string.IsNullOrEmpty(request.Level))
        {
            if (Enum.TryParse<UserLevel>(request.Level, true, out var userLevel))
            {
                user.Level = userLevel;
            }
            else
            {
                return BadRequest(new { message = "Invalid Level value." });
            }
        }

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok(new
            {
                success = true,
                message = "Profile updated successfully.",
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    favoriteDog = user.FavoriteDog.ToString(), // convert enum to string
                    level = user.Level.ToString() // convert enum to string
                }
            });
        }

        return BadRequest("Failed to update profile.");
    }




    public class ForgotPasswordDto
    {
        public string Email { get; set; }
    }

    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
    }
    public class SendOtpRequest
    {
        public string Email { get; set; }
    }

    public class VerifyOtpRequest
    {
        public string Email { get; set; }
        public string Otp { get; set; }
    }
    public class PatchProfileRequest
    {
        public string? FullName { get; set; }

        public string? FavoriteDog { get; set; } // Now expecting a string (e.g., "Pomeranian")

        public string? Level { get; set; } // Now expecting a string (e.g., "Beginner")
    }



}
