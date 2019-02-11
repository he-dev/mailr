using System;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace Mailr.Extensions.Utilities.Mvc
{
    public delegate IActionResult EmailViewCallback([AspMvcView] string viewName, object model);

    public static class ControllerExtensions
    {
        public static EmailViewCallback SelectEmailView(this Controller controller, EmailView view)
        {
            switch (view)
            {
                case EmailView.Original: return controller.PartialView;
                case EmailView.Embedded: return controller.View;
                default: throw new ArgumentOutOfRangeException(paramName: nameof(view), message: "Invalid email-view.");
            }
        }
    }
}