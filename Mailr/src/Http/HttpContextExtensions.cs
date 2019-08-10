using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;

namespace Mailr.Http
{
    internal static class HttpContextExtensions
    {
        public static string GetCorrelationId(this HttpContext context)
        {
            return
                context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationIds)
                    ? correlationIds.First()
                    : context.TraceIdentifier;
        }
    }
}