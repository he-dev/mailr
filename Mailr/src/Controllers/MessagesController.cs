using System;
using JetBrains.Annotations;
using Mailr.Extensions.Models;
using Mailr.Extensions.Utilities.Mvc;
using Mailr.Extensions.Utilities.Mvc.Filters;
using Mailr.Models;
using Mailr.Models.Test;
using Microsoft.AspNetCore.Mvc;
using Reusable.OmniLog.Mvc.Filters;

namespace Mailr.Controllers
{
    [Route("api/mailr/[controller]")]
    [Extension]
    public class MessagesController : Controller
    {
        // http://localhost:49471/api/mailr/messages/plaintext
        [HttpGet("[action]")]
        public IActionResult PlainText([FromQuery] EmailView view)
        {
            return this.EmailView(view)("~/src/Views/Mailr/Messages/PlainText.cshtml", "Hallo plain-text!");
        }

        [HttpPost("[action]")]
        [LogResponseBody]
        [SendEmail]
        public IActionResult PlainText([FromBody] Email<string> email)
        {
            return this.EmailView(EmailView.Original)("~/src/Views/Mailr/Messages/PlainText.cshtml", email.Body);
        }

        // http://localhost:49471/api/mailr/messages/test
        [HttpGet("[action]")]
        public IActionResult Test([FromQuery] EmailView view)
        {
            return this.EmailView(view)("~/src/Views/Mailr/Messages/Test.cshtml", new TestBody { Greeting = "Hallo test!" });
        }

        [HttpPost("[action]")]
        [LogResponseBody]
        [SendEmail]
        public IActionResult Test([FromBody] Email<TestBody> email)
        {
            return this.EmailView(EmailView.Original)("~/src/Views/Mailr/Messages/Test.cshtml", email.Body);
        }
    }
}
