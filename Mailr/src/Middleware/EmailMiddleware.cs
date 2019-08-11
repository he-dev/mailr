using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities;
using Mailr.Http;
using Mailr.Models;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Reusable.Beaver;
using Reusable.Data;
using Reusable.Extensions;
using Reusable.IOnymous;
using Reusable.IOnymous.Mail;
using Reusable.IOnymous.Mail.Smtp;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Nodes;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Quickey;

namespace Mailr.Middleware
{
    [UsedImplicitly]
    public class EmailMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IWorkItemQueue _workItemQueue;
        private readonly IResourceProvider _mailProvider;
        private readonly IConfiguration _configuration;

        public EmailMiddleware
        (
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IWorkItemQueue workItemQueue,
            IResourceProvider mailProvider,
            IConfiguration configuration
        )
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<EmailMiddleware>();
            _workItemQueue = workItemQueue;
            _mailProvider = mailProvider;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context, IFeatureToggle featureToggle)
        {
            var originalResponseBody = context.Response.Body;

            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);

                    if (context.Items.TryGetItem(HttpContextItems.Email, out var email))
                    {
                        // Selecting interface properties because otherwise the body will be dumped too.
                        _logger.Log(Abstraction.Layer.Business().Meta(new {EmailMetadata = new {email.From, email.To, email.Subject, email.IsHtml}}));

                        using (var responseBodyCopy = new MemoryStream())
                        {
                            // We need a copy of this because the internal handler might close it and we won't able to restore it.
                            responseBody.Rewind();
                            await responseBody.CopyToAsync(responseBodyCopy);
                            await SendEmailAsync(context, responseBodyCopy, email, featureToggle);
                        }

                        // Restore Response.Body
                        responseBody.Rewind();
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

        private async Task SendEmailAsync(HttpContext context, Stream responseBody, IEmail email, IFeatureToggle featureToggle)
        {
            //responseBody.Rewind();
            using (var reader = new StreamReader(responseBody.Rewind()))
            {
                var body = await reader.ReadToEndAsync();
                _workItemQueue.Enqueue(async cancellationToken =>
                {
                    // We need to rebuild the scope here because it'll be executed outside the request pipeline.
                    var scope = _logger.UseScope(); //.AttachElapsed().AttachUserCorrelationId(context).AttachUserAgent(context);

                    try
                    {
                        var smtpEmail = new Email<EmailSubject, EmailBody>
                        {
                            From = email.From ?? _configuration["Smtp:From"] ?? "unknown@email.com",
                            To = email.To,
                            CC = email.CC,
                            Subject = new EmailSubject {Value = email.Subject},
                            Body = new EmailBody {Value = body},
                            IsHtml = email.IsHtml,
                            Attachments = email.Attachments
                        };

                        var requestContext =
                            ImmutableContainer
                                .Empty
                                .SetItem(SmtpRequestContext.Host, _configuration["Smtp:Host"])
                                .SetItem(SmtpRequestContext.Port, int.Parse(_configuration["Smtp:Port"]));

                        await featureToggle.ExecuteAsync<IResource>
                        (
                            name: Features.SendEmail,
                            body: async () => await _mailProvider.SendEmailAsync(smtpEmail, requestContext)
                        );

                        //_logger.Log(Abstraction.Layer.Network().Routine(nameof(MailProviderExtensions.SendEmailAsync)).Completed());
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