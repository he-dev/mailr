using System;
using System.Linq;
using Mailr.Extensions.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Reflection;

namespace Mailr.Extensions.Utilities.Mvc.Filters
{
    using static ItemNames;

    /// <summary>
    /// Adds the action 'Email{T}' argument as IEmailMatadat to the http-context that the middleware sees and uses for sending emails.
    /// </summary>
    public class SendEmail : ActionFilterAttribute
    {
        private readonly ILogger<SendEmail> _logger;

        public SendEmail(ILogger<SendEmail> logger)
        {
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.Values.OfType<IEmailMetadata>().SingleOrDefault() is var emailMetadata && !(emailMetadata is null))
            {
                context.HttpContext.Items[EmailMetadata] = emailMetadata;

                if (bool.TryParse(context.HttpContext.Request.Query["isDesignMode"].FirstOrDefault(), out var isDesignMode))
                {
                    emailMetadata.CanSend = !isDesignMode;
                }
            }
            else
            {
                _logger.Log(Abstraction.Layer.Infrastructure().Routine("GetEmailMetadata").Faulted(), "EmailMetadata is null.");
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