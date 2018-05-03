using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mailr.Utilities.Mvc.Filters
{
    public class ExtensionId : ActionFilterAttribute
    {
        private readonly string _extensionId;
        private readonly bool _isInternal;

        public ExtensionId(Type assemblyProviderType)
        {
            _extensionId = Assembly.GetAssembly(assemblyProviderType).GetName().Name;
        }

        internal ExtensionId(string extensionId)
        {
            _extensionId = extensionId;
            _isInternal = true;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            context.HttpContext.ExtensionId(_extensionId);
            context.HttpContext.IsInternalExtension(_isInternal);
        }
    }
}