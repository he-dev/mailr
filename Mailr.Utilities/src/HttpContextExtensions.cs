using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Mailr.Utilities
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

        internal static void IsInternalExtension(this HttpContext context, bool isInternal)
        {
            context.Items[nameof(IsInternalExtension)] = isInternal;
        }

        internal static bool IsInternalExtension(this HttpContext context)
        {
            return context.Items[nameof(IsInternalExtension)] is bool isInternal && isInternal;
        }
    }
}
