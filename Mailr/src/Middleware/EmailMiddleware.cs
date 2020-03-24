﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Reusable.Beaver;
using Reusable.Data;
using Reusable.Extensions;
using Reusable.OmniLog;
using Reusable.OmniLog.Extensions;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Nodes;
using Reusable.Translucent;
using Reusable.Translucent.Controllers;
using Reusable.Translucent.Models;
using RequestDelegate = Microsoft.AspNetCore.Http.RequestDelegate;

namespace Mailr.Middleware
{
    [UsedImplicitly]
    public class EmailMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IWorkItemQueue _workItemQueue;
        private readonly IResource _resource;
        private readonly IConfiguration _configuration;

        public EmailMiddleware
        (
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IWorkItemQueue workItemQueue,
            IResource resource,
            IConfiguration configuration
        )
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<EmailMiddleware>();
            _workItemQueue = workItemQueue;
            _resource = resource;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context, IFeatureController featureController)
        {
            var originalResponseBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            if (context.Items.TryGetItem(HttpContextItems.Email, out var email))
            {
                // Selecting interface properties because otherwise the body will be dumped too.

                using (var responseBodyCopy = new MemoryStream())
                {
                    // We need a copy of this because the internal handler might close it and we won't able to restore it.
                    responseBody.Rewind();
                    await responseBody.CopyToAsync(responseBodyCopy);
                    await SendEmailAsync(responseBodyCopy, email, featureController);
                }

                // Restore Response.Body
                responseBody.Rewind();
                await responseBody.CopyToAsync(originalResponseBody);
                context.Response.Body = originalResponseBody;
            }
        }

        private async Task SendEmailAsync(Stream responseBody, IEmail email, IFeatureController featureController)
        {
            using var reader = new StreamReader(responseBody.Rewind());
            var body = await reader.ReadToEndAsync();
            _workItemQueue.Enqueue(async cancellationToken =>
            {
                var smtpEmail = new Email<EmailSubject, EmailBody>
                {
                    From = email.From ?? _configuration["Smtp:From"] ?? "unknown@email.com",
                    To = email.To,
                    CC = email.CC ?? new List<string>(),
                    Subject = new EmailSubject { Value = email.Subject },
                    Body = new EmailBody { Value = body },
                    IsHtml = email.IsHtml,
                    Attachments = email.Attachments ?? new Dictionary<string, byte[]>()
                };

                // We need to rebuild the scope here because it'll be executed outside the request pipeline.
                using var scope = _logger.BeginScope("SendEmail");
                _logger.Log(Execution.Context.Service().WorkItem("email", new { email.From, email.To, email.CC, email.Subject }));

                try
                {
                    using var response = await featureController.Use(Features.SendEmail, async () => await _resource.SendEmailAsync(smtpEmail, request =>
                    {
                        request.Host = _configuration["Smtp:Host"];
                        request.Port = int.Parse(_configuration["Smtp:Port"]);
                    }));
                }
                catch (Exception ex)
                {
                    _logger.Scope().Exceptions.Push(ex);
                }
            }, $"Subject: '{email.Subject}', To: {email.To.Join(", ")}");
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