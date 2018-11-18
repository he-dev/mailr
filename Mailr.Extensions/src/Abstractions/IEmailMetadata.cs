 namespace Mailr.Extensions.Abstractions
{
    public interface IEmailMetadata
    {
        string To { get; }

        string Subject { get; }

        bool IsHtml { get; }

        string Theme { get; }

        bool CanSend { get; }
    }
}