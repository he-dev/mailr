using System.Collections.Generic;
using JetBrains.Annotations;

namespace Mailr.Extensions.Abstractions
{
    public interface IEmailMetadata
    {
        [CanBeNull]
        string From { get; }

        [CanBeNull, ItemCanBeNull]
        IEnumerable<string> To { get; }

        // ReSharper disable once InconsistentNaming
        [CanBeNull, ItemCanBeNull]
        IEnumerable<string> CC { get; }

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