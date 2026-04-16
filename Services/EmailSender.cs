using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SchedulingApp.Services
{
    public class SmtpSettings
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Smart Scheduler";
    }

    public interface IEmailSender
    {
        bool IsConfigured { get; }
        Task<bool> SendReminderEmailAsync(string toEmail, string taskTitle, string message);
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;

        public SmtpEmailSender(IOptions<SmtpSettings> options)
        {
            _settings = options.Value;
        }

        public bool IsConfigured =>
            _settings.Enabled &&
            !string.IsNullOrWhiteSpace(_settings.Host) &&
            !string.IsNullOrWhiteSpace(_settings.FromEmail);

        public async Task<bool> SendReminderEmailAsync(string toEmail, string taskTitle, string message)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(toEmail))
            {
                return false;
            }

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.UseSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = $"[Smart Scheduler] Nhắc việc: {taskTitle}",
                Body = message,
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);
            await client.SendMailAsync(mail);
            return true;
        }
    }
}
