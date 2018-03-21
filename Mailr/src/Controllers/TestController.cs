using Mailr.Models;
using Mailr.Models.Test;
using Mailr.Utilities.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

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
        [LogResponseBody]
        [SendEmail]
        public IActionResult Test([FromBody] Email<TestBody> email)
        {
            return PartialView("~/src/Views/Emails/Test/Test.cshtml", email.Body);
        }
    }
}
