using Microsoft.AspNetCore.Identity;

namespace GeminiTest.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? GoogleId { get; set; }  // Store Google ID
    }
}
