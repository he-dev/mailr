using Mailr.Extensions.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Mailr.Extensions.Utilities
{
    public static class HttpContextExtensions
    {
        public static void ExtensionId(this HttpContext context, string extensionId)
        {
            context.Items[nameof(ExtensionId)] = extensionId;
        }

        public static string ExtensionId(this HttpContext context)
        {
            return (string)context.Items[nameof(ExtensionId)];
        }

        internal static void ExtensionType(this HttpContext context, ExtensionType extensionType)
        {
            context.Items[nameof(ExtensionType)] = extensionType;
        }

        internal static ExtensionType ExtensionType(this HttpContext context)
        {
            return context.Items[nameof(ExtensionType)] is ExtensionType extensionType ? extensionType : Extensions.ExtensionType.Undefined;
        }

        internal static IEmailMetadata EmailMetadata(this HttpContext context)
        {
            return context.Items[ItemNames.EmailMetadata] is IEmailMetadata emailMetadata ? emailMetadata : default(IEmailMetadata);
        }
    }
}
