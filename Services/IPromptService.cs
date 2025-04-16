namespace GeminiTest.Services
{
    public interface IPromptService
    {
        string GetPromptByDog(string dogBreed, string word, string sentence, string meaning);
    }
}
