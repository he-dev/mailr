using System;
using Mailr.Models.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Utilities.Mvc.Filters
{
    public class SendEmail : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("email", out var obj) && obj is IEmailMetadata email)
            {
                context.HttpContext.Items["EmailMetadata"] = email;
            }
            else
            {
                throw new InvalidOperationException("'email' argument not found.");
            }
        }
    }
}