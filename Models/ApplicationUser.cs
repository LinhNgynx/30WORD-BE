using Microsoft.AspNetCore.Identity;
using System.Data;

namespace GeminiTest.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; } = "User Default Name";

        public DogType FavoriteDog { get; set; } = DogType.Pomeranian;
        public UserLevel Level { get; set; } = UserLevel.Beginner;
    }
    public enum DogType
    {
        Pomeranian,
        Shiba,
        Golden,
        Wolf
    }
    public enum UserLevel
    {
        Beginner,
        Intermediate,
        Advanced
    }


}
