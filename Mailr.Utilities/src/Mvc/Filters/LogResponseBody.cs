using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Utilities.Mvc.Filters
{
    public class LogResponseBody : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            context.HttpContext.Items["ResponseBodyLoggingEnabled"] = true;
        }
    }
}
