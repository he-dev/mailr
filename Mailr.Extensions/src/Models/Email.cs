using System;
using System.Collections.Generic;
using Mailr.Extensions.Abstractions;

namespace Mailr.Extensions.Models
{
    public class Email<TBody> : IEmail
    {
        public string Id { get; set; } = Guid.NewGuid().ToString().ToUpper();

        public string From { get; set; }

        public List<string> To { get; set; } = new List<string>();

        public List<string> CC { get; set; } = new List<string>();

        public string Subject { get; set; }

        public TBody Body { get; set; }

        public Dictionary<string, byte[]> Attachments { get; set; } = new Dictionary<string, byte[]>();

        public bool IsHtml { get; set; }

        public string Theme { get; set; }
    }
}