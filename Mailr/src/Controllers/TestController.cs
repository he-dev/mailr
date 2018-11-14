using System;
using JetBrains.Annotations;
using Mailr.Extensions.Models;
using Mailr.Extensions.Utilities.Mvc;
using Mailr.Extensions.Utilities.Mvc.Filters;
using Mailr.Models;
using Mailr.Models.Test;
using Microsoft.AspNetCore.Mvc;
using Reusable.AspNetCore.Http.Mvc.Filters;

namespace Mailr.Controllers
{
    [Route("api/mailr/[controller]")]
    [Extension]
    public class TestController : Controller
    {
        // http://localhost:49471/mailr/test/test
        [HttpGet("[action]")]
        public IActionResult Test(bool embedded)
        {
            return this.EmailView(embedded)("~/src/Views/Mailr/Test/Test.cshtml", new TestBody { Greeting = "Hallo Mailr!" });
        }

        [HttpPost("[action]")]
        [LogResponseBody]
        [SendEmail]
        public IActionResult Test([FromBody] Email<TestBody> email)
        {
            return this.EmailView(embedded: false)("~/src/Views/Mailr/Test/Test.cshtml", email.Body);
        }
    }
}
