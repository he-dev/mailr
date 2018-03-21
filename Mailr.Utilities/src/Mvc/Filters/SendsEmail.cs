using Mailr.Models.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Utilities.Mvc.Filters
{
    public class SendsEmail : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("email", out var obj) && obj is IEmailMetadata email)
            {
                context.HttpContext.Items["EmailMetadata"] = email;
            }
        }
    }
}