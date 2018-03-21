using Mailr.Models.Abstractions;

namespace Mailr.Models
{
    public class Email<TBody> : IEmailMetadata
    {
        public string To { get; set; }

        public string Subject { get; set; }

        public TBody Body { get; set; }

        public bool IsHtml { get; set; }
    }

    public static class Email
    {
        public static Email<TBody> Create<TBody>(string to, string subject, TBody body)
        {
            return new Email<TBody>
            {
                To = to,
                Subject = subject,
                Body = body
            };
        }
    }
}