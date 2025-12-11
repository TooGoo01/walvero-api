using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class MailService : IMailService
    {

        private readonly IConfiguration _configuration;

        public MailService(IConfiguration configuration)
        {
            _configuration = configuration;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

        }

        public async Task SendEmailConfirmMail(string to, string userName, string confirmToken)
        {
            StringBuilder mail = new();
            mail.AppendLine("Salam<br>Hesabınızı təsdiqləmək üçün aşağıdakı linkə keçid edin.<br><strong><a target=\"blank\" href=\"");
            mail.AppendLine(_configuration["MaxOilClientUrl"]);
            mail.Append("/EmailConfirm/");
            mail.AppendLine(userName);
            mail.AppendLine("/");
            mail.AppendLine(confirmToken);
            mail.AppendLine("\">Reset Password</a></strong>");
            await SendMailAsync(to, "Reset Password", mail.ToString());
        }

        public async Task SendMailAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            await SendMailAsync(new[] { to }, subject, body, isBodyHtml);
        }

        public async Task SendMailAsync(string[] tos, string subject, string body, bool isBodyHtml = true)
        {
            MailMessage mail = new();
            mail.IsBodyHtml = isBodyHtml;
            foreach (var to in tos)
                mail.To.Add(to);
            mail.Subject = subject;
            mail.Body = body;
            mail.From = new(_configuration["EmailSender:Username"], "Info InStudy", System.Text.Encoding.UTF8);
            SmtpClient smtp = new();
            smtp.Credentials = new NetworkCredential(_configuration["EmailSender:Username"], _configuration["EmailSender:Password"]);
            smtp.Port = int.Parse(_configuration["EmailSender:Port"]);
            smtp.EnableSsl = true;
            smtp.Host = _configuration["EmailSender:Host"];
            await smtp.SendMailAsync(mail);
        }

        /*  public async Task SendMailAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            await SendMailAsync(new[] { to }, subject, body, isBodyHtml);
        }

        public async Task SendMailAsync(string[] tos, string subject, string body, bool isBodyHtml = true)
        {
            MailMessage mail = new();
            mail.IsBodyHtml = isBodyHtml;
            foreach (var to in tos)
                mail.To.Add(to);
            mail.Subject = subject;
            mail.Body = body;
            //mail.Attachments.Add(new Attachment(fileName));
            mail.From = new(_configuration["EmailSender:Username"], "Faina Zuckerman", System.Text.Encoding.UTF8);
            SmtpClient smtp = new();
            smtp.Credentials = new NetworkCredential(_configuration["EmailSender:Username"], _configuration["EmailSender:Password"]);
            smtp.Port = int.Parse(_configuration["EmailSender:Port"]);
            smtp.Host = _configuration["EmailSender:Host"];
            await smtp.SendMailAsync(mail);
        }
*/

        public async Task SendPasswordResetMail(string to, string userName, string resetToken)
        {
            StringBuilder mail = new();
            mail.AppendLine("Salam<br>Şifrənizi yeniləmək üçün aşağıdaki linkə klikləyin.<br><strong><a target=\"blank\" href=\"");
            mail.AppendLine(_configuration["EmailSender"]);
            mail.Append("/ResetPassword/");
            mail.AppendLine(userName);
            mail.AppendLine("/");
            mail.AppendLine(resetToken);
            mail.AppendLine("\">Reset Password</a></strong>");
            await SendMailAsync(to, "Reset Password", mail.ToString());


        }
    }
}
