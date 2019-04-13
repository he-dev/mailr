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

        internal static void ControllerType(this HttpContext context, ControllerType controllerType)
        {
            context.Items[nameof(ControllerType)] = controllerType;
        }

        internal static ControllerType ControllerType(this HttpContext context)
        {
            return
                context.Items[nameof(ControllerType)] is ControllerType extensionType
                    ? extensionType
                    : Extensions.ControllerType.Undefined;
        }

        internal static IEmailMetadata EmailMetadata(this HttpContext context)
        {
            return context.Items[ItemNames.EmailMetadata] is IEmailMetadata emailMetadata ? emailMetadata : default;
        }
    }
}