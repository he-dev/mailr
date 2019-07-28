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
        private readonly IFeatureToggle _featureToggle;

        public EmailMiddleware
        (
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IWorkItemQueue workItemQueue,
            IResourceProvider mailProvider,
            IConfiguration configuration,
            IFeatureToggle featureToggle
        )
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<EmailMiddleware>();
            _workItemQueue = workItemQueue;
            _mailProvider = mailProvider;
            _configuration = configuration;
            _featureToggle = featureToggle;
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

                    if (context.Items.TryGetItem(HttpContextItems.Email, out var email))
                    {
                        // Selecting interface properties because otherwise the body will be dumped too.
                        _logger.Log(Abstraction.Layer.Business().Meta(new { EmailMetadata = new { email.From, email.To, email.Subject, email.IsHtml } }));

                        using (var responseBodyCopy = new MemoryStream())
                        {
                            // We need a copy of this because the internal handler might close it and we won't able to restore it.
                            responseBody.Rewind();
                            await responseBody.CopyToAsync(responseBodyCopy);
                            await SendEmailAsync(context, responseBodyCopy, email);
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

        private async Task SendEmailAsync(HttpContext context, Stream responseBody, IEmail email)
        {
            //responseBody.Rewind();
            using (var reader = new StreamReader(responseBody.Rewind()))
            {
                var body = await reader.ReadToEndAsync();
                _workItemQueue.Enqueue(async cancellationToken =>
                {
                    // We need to rebuild the scope here because it'll be executed outside the request pipeline.
                    var scope = _logger.BeginScope().AttachElapsed().AttachUserCorrelationId(context).AttachUserAgent(context);

                    try
                    {
                        //if (email.CanSend)
                        {
                            //_logger.Log(Abstraction.Layer.Service().Decision("Send email.").Because("Sending emails is enabled."));

                            var smtpEmail = new Email<EmailSubject, EmailBody>
                            {
                                From = email.From ?? _configuration["Smtp:From"] ?? "unknown@email.com",
                                To = email.To,
                                CC = email.CC,
                                Subject = new EmailSubject { Value = email.Subject },
                                Body = new EmailBody { Value = body },
                                IsHtml = email.IsHtml,
                                Attachments = email.Attachments
                            };

                            var requestContext =
                                ImmutableContainer
                                    .Empty
                                    .SetItem(SmtpRequestContext.Host, _configuration["Smtp:Host"])
                                    .SetItem(SmtpRequestContext.Port, int.Parse(_configuration["Smtp:Port"]));

                            //await _mailProvider.SendEmailAsync(smtpEmail, requestContext);

                            await _featureToggle.ExecuteAsync<IResource>
                            (
                               name: Features.SendEmail.Index(email.Id),
                               body: async () => await _mailProvider.SendEmailAsync(smtpEmail, requestContext)
                               //fallback: () => Task.FromResult<IResource>(default)
                            );

                            //_logger.Log(Abstraction.Layer.Network().Routine(nameof(MailProviderExtensions.SendEmailAsync)).Completed());
                        }
                        //else
                        {
                            //_logger.Log(Abstraction.Layer.Service().Decision("Don't send email.").Because("Sending emails is disabled.").Warning());
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