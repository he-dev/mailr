﻿using System;
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
using Reusable.IOnymous;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Middleware
{
    using static ItemNames;

    public class EmailMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly ILogger _logger;

        private readonly IWorkItemQueue _workItemQueue;

        private readonly IResourceProvider _mailProvider;

        public EmailMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IWorkItemQueue workItemQueue, IResourceProvider mailProvider)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<EmailMiddleware>();
            _workItemQueue = workItemQueue;
            _mailProvider = mailProvider;
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
                            await _mailProvider.SendEmailAsync(new Email<EmailSubject, EmailBody>
                            {
                                To = emailMetadata.To,
                                CC = emailMetadata.CC,
                                Subject = new EmailSubject { Value = emailMetadata.Subject },
                                Body = new EmailBody { Value = body },
                                IsHtml = emailMetadata.IsHtml
                            });
                            _logger.Log(Abstraction.Layer.Network().Routine(nameof(MailProviderExtensions.SendEmailAsync)).Completed());
                        }
                        else
                        {
                            _logger.Log(Abstraction.Layer.Network().Routine(nameof(MailProviderExtensions.SendEmailAsync)).Canceled(), "Email sending disabled.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(Abstraction.Layer.Network().Routine(nameof(MailProviderExtensions.SendEmailAsync)).Faulted(), ex);
                    }
                    finally
                    {
                        scope.Dispose();
                    }
                });
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