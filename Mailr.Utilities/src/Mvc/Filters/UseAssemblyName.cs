using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Utilities.Mvc.Filters
{
    public class UseAssemblyName : ActionFilterAttribute
    {
        private readonly Type _assemblyProviderType;

        public UseAssemblyName(Type assemblyProviderType)
        {
            _assemblyProviderType = assemblyProviderType;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            context.HttpContext.Items["Extension"] = Assembly.GetAssembly(_assemblyProviderType).GetName().Name;
        }
    }
}