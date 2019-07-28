using System;
using System.Linq;
using Mailr.Extensions.Abstractions;
using Mailr.Extensions.Utilities.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Reusable.Beaver;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Quickey;
using Reusable.Reflection;

namespace Mailr.Extensions.Utilities.Mvc.Filters
{
    /// <summary>
    /// Adds the action 'Email{T}' argument as IEmailMetadata to the http-context that the middleware sees and uses for sending emails.
    /// </summary>
    public class SendEmail : ActionFilterAttribute
    {
        private readonly ILogger _logger;
        private readonly IFeatureToggle _featureToggle;

        public SendEmail(ILogger<SendEmail> logger, IFeatureToggle featureToggle)
        {
            _logger = logger;
            _featureToggle = featureToggle;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.Values.OfType<IEmail>().SingleOrDefault() is var email && !(email is null))
            {
                context.HttpContext.Items.SetItem(HttpContextItems.Email, email);
                context.HttpContext.Items.SetItem(HttpContextItems.EmailTheme, email.Theme);

                if (bool.TryParse(context.HttpContext.Request.Query[QueryStringNames.IsDesignMode].FirstOrDefault(), out var isDesignMode))
                {
                    //email.CanSend = !isDesignMode;
                    if (isDesignMode)
                    {
                        _featureToggle.With(Features.SendEmail.Index(email.Id), f => f.Disable().EnableToggler());
                    }
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