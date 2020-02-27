﻿using System.Linq;
using Mailr.Extensions.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Reusable.Beaver;
using Reusable.Beaver.Policies;
using Reusable.OmniLog.Abstractions;

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
            if (context.ActionArguments.Values.OfType<IEmail>().SingleOrDefault() is {} email)
            {
                context.HttpContext.Items.SetItem(HttpContextItems.Email, email);
                context.HttpContext.Items.SetItem(HttpContextItems.EmailTheme, email.Theme);

                if (bool.TryParse(context.HttpContext.Request.Query[QueryStringNames.Design].FirstOrDefault(), out var design) && design)
                {
                    _featureToggle.Disable(Features.SendEmail);
                }
            }
        }
    }
}