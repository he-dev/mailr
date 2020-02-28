using System.Collections.Generic;
using JetBrains.Annotations;

namespace Mailr.Extensions.Abstractions
{
    public interface IEmail
    {
        string Id { get; }
        
        string From { get; }
        
        List<string> To { get; }

        // ReSharper disable once InconsistentNaming
        List<string> CC { get; }

        string Subject { get; }

        Dictionary<string, byte[]> Attachments { get; }

        bool IsHtml { get; }

        string? Theme { get; }
    }
}