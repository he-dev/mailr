using System;
using System.Linq;
using Mailr.Extensions.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Reusable.Reflection;

namespace Mailr.Extensions.Utilities.Mvc.Filters
{
    using static ItemNames;

    /// <summary>
    /// Adds the action 'Email{T}' argument as IEmailMatadat to the http-context that the middleware sees and uses for sending emails.
    /// </summary>
    public class SendEmail : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var emailMetadata = context.ActionArguments.Values.OfType<IEmailMetadata>().SingleOrDefault();
            if (emailMetadata is null)
            {
                throw DynamicException.Create("EmailArgumentNotFound", $"Could not read the email metadata for '{context.HttpContext.Request.Path.Value}'. This might be due to an invalid model.");
            }

            context.HttpContext.Items[EmailMetadata] = emailMetadata;

            //if (bool.TryParse(context.HttpContext.Request.Query["IsPreview"].FirstOrDefault(), out var isPreview))
            //{
            //    context.HttpContext.Items["IsPreview"] = isPreview;
            //}
        }
    }
}