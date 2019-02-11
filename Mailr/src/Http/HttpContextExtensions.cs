using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Reusable.OmniLog;
using Reusable.OmniLog.Attachments;

namespace Mailr.Http
{
    internal static class HttpContextExtensions
    {
        public static T AttachUserAgent<T>(this T scope, HttpContext context) where T : ILogScope
        {
            if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                scope.With(new Lambda("Product", _ => Regex.Replace(userAgent.First(), "\\/", "-v")));
            }

            return scope;
        }

        public static T AttachUserCorrelationId<T>(this T scope, HttpContext context) where T : ILogScope
        {
            var correlationId = context.TraceIdentifier;

            if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationIds))
            {
                correlationId = correlationIds.First();
            }

            scope.WithCorrelationId(correlationId);
            return scope;
        }
    }
}