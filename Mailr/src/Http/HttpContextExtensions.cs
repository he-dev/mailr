using System.Linq;
using Microsoft.AspNetCore.Http;

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