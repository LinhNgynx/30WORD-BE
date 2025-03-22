using System.Threading.Tasks;
namespace GeminiTest.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}
