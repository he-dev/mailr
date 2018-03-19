using Mailr.Extensions;
using Mailr.Middleware;
using Mailr.Models;
using Mailr.Models.Test;
using Microsoft.AspNetCore.Mvc;
using Reusable.AspNetCore.Middleware;

namespace Mailr.Controllers
{
    [Route("api/emails/[controller]")]
    public class TestController : Controller
    {
        //private const string Action

        // http://localhost:49471/preview/telexprocessor/cancellationreport
        [HttpGet("[action]")]
        public IActionResult Test()
        {
            return View("~/src/Views/Emails/Test/Test.cshtml", new TestBody { Greeting = "Hallo preview!" });
        }

        [HttpPost("[action]")]
        public IActionResult Test([FromBody] Email<TestBody> email)
        {
            HttpContext.Email(email.To, email.Subject);
            HttpContext.EnableResponseBodyLogging();
            return PartialView("~/src/Views/Emails/Test/Test.cshtml", email.Body);
        }
    }
}
