using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities;
using Mailr.Models;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Reusable.Net.Mail;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Middleware
{
    using static ItemNames;

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
            var correlationId = context.Request.Headers["X-Correlation-ID"].SingleOrDefault() ?? context.TraceIdentifier;

            var bodyBackup = context.Response.Body;

            using (var memory = new MemoryStream())
            {
                context.Response.Body = memory;

                await _next(context);

                memory.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(memory))
                {
                    var body = await reader.ReadToEndAsync();

                    if (context.Items[EmailMetadata] is IEmailMetadata emailMetadata)
                    {

                        _workItemQueue.Enqueue(async cancellationToken =>
                        {
                            var scope = _logger.BeginScope().WithCorrelationId(correlationId).AttachElapsed();
                            
                            // Selecting interface properties because otherwise the body will be dumped too.
                            _logger.Log(Abstraction.Layer.Business().Meta(new { emailMetadata = new { emailMetadata.To, emailMetadata.Subject, emailMetadata.IsHtml } }));

                            try
                            {
                                await _emailClient.SendAsync(new Email<EmailSubject, EmailBody>
                                {
                                    To = emailMetadata.To,
                                    Subject = new PlainTextSubject(emailMetadata.Subject),
                                    Body =
                                        emailMetadata.IsHtml
                                            ? (EmailBody)new ParialViewEmailBody(body)
                                            : (EmailBody)new PlainTextBody(body),
                                });
                                _logger.Log(Abstraction.Layer.Network().Routine(nameof(IEmailClient.SendAsync)).Completed());
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(Abstraction.Layer.Network().Routine(nameof(IEmailClient.SendAsync)).Faulted(), ex);
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