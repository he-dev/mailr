using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities;
using Mailr.Http;
using Mailr.Models;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.sdk.Mail;
using Reusable.sdk.Mail.Models;

namespace Mailr.Middleware
{
    using static ItemNames;

    public class EmailMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly ILogger _logger;

        private readonly IWorkItemQueue _workItemQueue;

        private readonly IEmailClient _emailClient;

        public EmailMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IWorkItemQueue workItemQueue, IEmailClient emailClient)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<EmailMiddleware>();
            _workItemQueue = workItemQueue;
            _emailClient = emailClient;
        }

        public async Task Invoke(HttpContext context)
        {
            var originalResponseBody = context.Response.Body;

            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);

                    if (context.Items[EmailMetadata] is IEmailMetadata emailMetadata)
                    {
                        // Selecting interface properties because otherwise the body will be dumped too.
                        _logger.Log(Abstraction.Layer.Business().Meta(new { EmailMetadata = new { emailMetadata.To, emailMetadata.Subject, emailMetadata.IsHtml } }));

                        using (var responseBodyCopy = new MemoryStream())
                        {
                            // We need a copy of this because the internal handler might close it and we won't able to restore it.
                            responseBody.Seek(0, SeekOrigin.Begin);
                            await responseBody.CopyToAsync(responseBodyCopy);
                            await SendEmailAsync(context, responseBodyCopy, emailMetadata);
                        }

                        // Restore Response.Body
                        responseBody.Seek(0, SeekOrigin.Begin);
                        await responseBody.CopyToAsync(originalResponseBody);
                        context.Response.Body = originalResponseBody;
                    }
                }
                catch (Exception inner)
                {
                    _logger.Log(Abstraction.Layer.Network().Routine("next").Faulted(), inner);
                }
            }
        }

        private async Task SendEmailAsync(HttpContext context, Stream responseBody, IEmailMetadata emailMetadata)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(responseBody))
            {
                var body = await reader.ReadToEndAsync();
                _workItemQueue.Enqueue(async cancellationToken =>
                {
                    // We need to rebuild the scope here because it'll be executed outside the request pipeline.
                    var scope = _logger.BeginScope().AttachElapsed().AttachUserCorrelationId(context).AttachUserAgent(context);

                    try
                    {
                        if (emailMetadata.CanSend)
                        {
                            await _emailClient.SendAsync(new Email<EmailSubject, EmailBody>
                            {
                                To = emailMetadata.To,
                                Subject = new PlainTextSubject(emailMetadata.Subject),
                                Body =
                                    emailMetadata.IsHtml
                                        ? (EmailBody)new HtmlEmailBody(body)
                                        : (EmailBody)new PlainTextBody(body),
                            });
                            _logger.Log(Abstraction.Layer.Network().Routine(nameof(IEmailClient.SendAsync)).Completed());
                        }
                        else
                        {
                            _logger.Log(Abstraction.Layer.Network().Routine(nameof(IEmailClient.SendAsync)).Canceled(), "Email sending disabled.");
                        }
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
        }

        private class HtmlEmailBody : EmailBody
        {
            private readonly string _body;

            public HtmlEmailBody(string body)
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

    public static class EmailMiddlewareExtensions
    {
        public static IApplicationBuilder UseEmail(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<EmailMiddleware>();
        }
    }
}