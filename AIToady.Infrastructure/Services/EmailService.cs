using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AIToady.Infrastructure.Services
{
    public class EmailService
    {
        private string _account;
        private string _password;

        public EmailService(string account, string password)
        {
            _account = account;
            _password = password;
        }
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient("smtp.ethereal.email", 587)
                {
                    Credentials = new NetworkCredential(_account, _password),
                    EnableSsl = true,
                    UseDefaultCredentials = false
                };

                var message = new MailMessage("test@ethereal.email", toEmail, subject, body);
                await client.SendMailAsync(message);
            }
            catch
            {
                // Silently fail for testing
            }
        }
    }
}