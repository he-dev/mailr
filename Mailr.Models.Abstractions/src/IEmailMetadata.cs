 namespace Mailr.Models.Abstractions
{
    public interface IEmailMetadata
    {
        string To { get; }

        string Subject { get; }

        bool IsHtml { get; }
    }
}