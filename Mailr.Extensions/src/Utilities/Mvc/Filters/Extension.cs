using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Extensions.Utilities.Mvc.Filters
{
    /// <summary>
    /// This action filter adds information about the extension to the current http-context.
    /// </summary>
    public class Extension : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // ReSharper disable once PossibleNullReferenceException - I'm pretty sure DeclaringType is never null.
            var assemblyName = ((ControllerActionDescriptor)context.ActionDescriptor).MethodInfo.DeclaringType.Assembly.GetName().Name;
            context.HttpContext.ExtensionId(assemblyName);
            context.HttpContext.ControllerType(assemblyName == "Mailr" ? ControllerType.Internal : ControllerType.External);
        }
    }
}