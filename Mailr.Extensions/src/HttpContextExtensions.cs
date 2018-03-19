using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Mailr.Extensions
{
    public static class HttpContextExtensions
    {
        public static void Email(this HttpContext context, string to, string subject, bool isHtml = true)
        {
            context.Items["Mailer.To"] = to;
            context.Items["Mailer.Subject"] = subject;
            context.Items["Mailer.IsHtml"] = isHtml;
        }

        public static (string To, string Subject, bool? IsHtml) Email(this HttpContext context)
        {
            return (
                context.Items.TryGetValue("Mailer.To", out var to) ? (string)to : null,
                context.Items.TryGetValue("Mailer.Subject", out var subject) ? (string)subject : null,
                context.Items.TryGetValue("Mailer.IsHtml", out var isHtml) ? (bool)isHtml : default(bool?)
            );
        }
    }
}
