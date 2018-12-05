using System.ComponentModel;
using JetBrains.Annotations;
using Mailr.Extensions.Abstractions;

namespace Mailr.Extensions.Models
{
    public class Email<TBody> : IEmailMetadata
    {
        public string To { get; set; }

        public string Subject { get; set; }

        public TBody Body { get; set; }

        public bool IsHtml { get; set; }

        public string Theme { get; set; }

        [DefaultValue(true)]
        public bool CanSend { get; set; } = true;
    }

    [PublicAPI]
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