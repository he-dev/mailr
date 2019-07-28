using System.Collections.Generic;
using JetBrains.Annotations;

namespace Mailr.Extensions.Abstractions
{
    public interface IEmail
    {
        [NotNull]
        string Id { get; }
        
        [CanBeNull]
        string From { get; }

        [CanBeNull, ItemCanBeNull]
        List<string> To { get; }

        // ReSharper disable once InconsistentNaming
        [CanBeNull, ItemCanBeNull]
        List<string> CC { get; }

        [CanBeNull]
        string Subject { get; }

        [CanBeNull]
        Dictionary<string, byte[]> Attachments { get; }

        bool IsHtml { get; }

        [CanBeNull]
        string Theme { get; }

        bool CanSend { get; set; }
    }
}