using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Attachments;

namespace Mailr.Http
{
    internal static class HttpContextExtensions
    {
        public static T AttachUserAgent<T>(this T scope, HttpContext context) where T : ILogScope
        {
            if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                var product = new Lambda("Product", _ => Regex.Replace(userAgent.First(), "\\/", "-v"));
                scope.Add(product.Name, product);
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

            scope.CorrelationId(correlationId);
            return scope;
        }
    }
}