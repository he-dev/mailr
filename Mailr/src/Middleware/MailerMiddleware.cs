using System;
using System.IO;
using System.Threading.Tasks;
using Mailr.Extensions;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Reusable.AspNetCore.Middleware;
using Reusable.Net.Mail;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Middleware
{
    public class MailerMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly ILogger _logger;

        private readonly IWorkItemQueue _workItemQueue;

        private readonly IEmailClient _emailClient;

        public MailerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IWorkItemQueue workItemQueue, IEmailClient emailClient)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<MailerMiddleware>();
            _workItemQueue = workItemQueue;
            _emailClient = emailClient;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlation = context.CorrelationObject();

            var bodyBackup = context.Response.Body;

            using (var memory = new MemoryStream())
            {
                context.Response.Body = memory;

                await _next(context);

                memory.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(memory))
                {
                    var body = await reader.ReadToEndAsync();

                    var email = context.Email();

                    if (!(email.To is null || email.Subject is null))
                    {
                        _workItemQueue.Enqueue(async cancellationToken =>
                        {
                            var scope = _logger.BeginScope(nameof(IEmailClient.SendAsync), correlation);
                            _logger.Log(Abstraction.Layer.Network().Data().Variable(new { email.To, email.Subject }));
                            try
                            {
                                await _emailClient.SendAsync(new Email<EmailSubject, EmailBody>
                                {
                                    To = email.To,
                                    Subject = new PlainTextSubject(email.Subject),
                                    Body =
                                        email.IsHtml.HasValue && email.IsHtml.Value
                                            ? (EmailBody)new ParialViewEmailBody(body)
                                            : (EmailBody)new PlainTextBody(body),
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(Abstraction.Layer.Network().Action().Failed(nameof(IEmailClient.SendAsync)), ex);
                            }
                            finally
                            {
                                scope.Dispose();
                            }
                        });
                    }

                    // Restore Response.Body
                    memory.Seek(0, SeekOrigin.Begin);
                    await memory.CopyToAsync(bodyBackup);
                    context.Response.Body = bodyBackup;
                }
            }
        }

        private class ParialViewEmailBody : EmailBody
        {
            private readonly string _body;

            public ParialViewEmailBody(string body)
            {
                _body = body;
                IsHtml = true;
                Encoding = System.Text.Encoding.UTF8;
            }
            public override string ToString()
            {
                return _body;
            }
        }
    }

    public static class MailerMiddlewareExtensions
    {
        public static IApplicationBuilder UseMailer(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MailerMiddleware>();
        }
    }
}