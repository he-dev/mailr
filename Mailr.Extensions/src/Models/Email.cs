using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using Mailr.Extensions.Abstractions;

namespace Mailr.Extensions.Models
{
    public class Email<TBody> : IEmail
    {
        public string From { get; set; }

        public List<string> To { get; set; }

        public List<string> CC { get; set; }

        public string Subject { get; set; }

        public TBody Body { get; set; }

        public Dictionary<string, byte[]> Attachments { get; set; }

        public bool IsHtml { get; set; }

        public string Theme { get; set; }

        [DefaultValue(true)]
        public bool CanSend { get; set; } = true;
    }

//    [PublicAPI]
//    public static class Email
//    {
//        public static Email<TBody> Create<TBody>(string from, IEnumerable<string> to, IEnumerable<string> cc, string subject, TBody body)
//        {
//            return new Email<TBody>
//            {
//                From = from,
//                To = to.ToList(),
//                CC = cc.ToList(),
//                Subject = subject,
//                Body = body
//            };
//        }
//    }
}