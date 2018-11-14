using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace Mailr.Extensions.Utilities.Mvc
{
    public delegate IActionResult EmailView([AspMvcView] string name, object model);

    public static class ControllerExtensions
    {
        public static EmailView EmailView(this Controller controller, bool embedded)
        {
            return
                embedded
                    ? (EmailView)controller.View
                    : (EmailView)controller.PartialView;
        }
    }
}