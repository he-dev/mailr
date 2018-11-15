using System;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace Mailr.Extensions.Utilities.Mvc
{
    public delegate IActionResult EmailViewCallback([AspMvcView] string name, object model);

    public static class ControllerExtensions
    {
        public static EmailViewCallback EmailView(this Controller controller, EmailView view)
        {
            switch (view)
            {
                case Mvc.EmailView.Original: return (EmailViewCallback)controller.PartialView;
                case Mvc.EmailView.Embedded: return (EmailViewCallback)controller.View;
                default: throw new ArgumentOutOfRangeException(paramName: nameof(view), message: "Invalid email-view.");
            }
        }
    }

    public enum EmailView
    {
        Original,
        Embedded,
    }
}