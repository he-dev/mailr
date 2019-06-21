﻿using System;
using System.Linq;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Reflection;

namespace Mailr.Extensions.Utilities.Mvc.Filters
{
    using static HttpContextItemNames;

    /// <summary>
    /// Adds the action 'Email{T}' argument as IEmailMetadata to the http-context that the middleware sees and uses for sending emails.
    /// </summary>
    public class SendEmail : ActionFilterAttribute
    {
        private readonly ILogger _logger;

        public SendEmail(ILogger<SendEmail> logger)
        {
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.Values.OfType<IEmail>().SingleOrDefault() is var emailMetadata && !(emailMetadata is null))
            {
                context.HttpContext.Items[EmailMetadata] = emailMetadata;
                context.HttpContext.Items[EmailTheme] = emailMetadata.Theme;

                if (bool.TryParse(context.HttpContext.Request.Query[QueryStringNames.IsDesignMode].FirstOrDefault(), out var isDesignMode))
                {
                    emailMetadata.CanSend = !isDesignMode;
                }
            }
            else
            {
                //_logger.Log(Abstraction.Layer.Service().Routine("GetEmailMetadata").Faulted(), "EmailMetadata is null.");
                //throw DynamicException.Create
                //(
                //    $"{((ControllerActionDescriptor)context.ActionDescriptor).ActionName}ActionArgument",
                //    $"Could not read the email metadata for '{context.HttpContext.Request.Path.Value}'. " +
                //    $"This might be due to an invalid model. " +
                //    $"Try using the '[ServiceFilter(typeof(ValidateModel))]' filter to avoid this and to validate the model."
                //);
            }
        }
    }
}