using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mailr.Extensions.Models;
using Mailr.Extensions.Utilities.Mvc;
using Mailr.Extensions.Utilities.Mvc.Filters;
using Mailr.Extensions.Utilities.Mvc.ModelBinding;
using Mailr.Models;
using Mailr.Models.Test;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Reusable.OmniLog.SemanticExtensions.AspNetCore.Mvc.Filters;
using Reusable.Utilities.AspNetCore.ActionFilters;

namespace Mailr.Controllers
{
    //[ApiVersion("3.0")]
    [Area("mailr")]
    [Route("api/v{version:apiVersion}/mailr/[controller]")]
    //[Extension]
    [ApiController]
    public class MessagesController : Controller
    {
        // http://localhost:49471/api/mailr/messages/plaintext
        //[HttpGet("[action]")]
        //public IActionResult PlainText([FromQuery] EmailView view)
        //{
        //    return this.EmailView(view)("~/src/Views/Mailr/Messages/PlainText.cshtml", "Hallo plain-text!");
        //}

        [HttpPost("[action]")]
        [ServiceFilter(typeof(LogResponseBody))]
        [ServiceFilter(typeof(ValidateModel))]
        [ServiceFilter(typeof(SendEmail))]
        public IActionResult PlainText([FromBody] Email<string> email, [ModelBinder(typeof(EmailViewBinder))] EmailView view)
        {
            return this.SelectEmailView(view)("~/src/Views/Mailr/Messages/PlainText.cshtml", email.Body);
        }

        // http://localhost:49471/api/mailr/messages/test
        //[HttpGet("[action]")]
        //public IActionResult Test([FromQuery] EmailView view)
        //{
        //    return this.EmailView(view)("~/src/Views/Mailr/Messages/Test.cshtml", new TestBody { Greeting = "Hallo test!" });
        //}

        [HttpPost("[action]")]
        [ServiceFilter(typeof(LogResponseBody))]
        [ServiceFilter(typeof(ValidateModel))]
        [ServiceFilter(typeof(SendEmail))]
        public IActionResult Test([FromBody] Email<TestBody> email, [ModelBinder(typeof(EmailViewBinder))] EmailView view)
        {
            //email.Body = new TestBody { Greeting = "Version 3.0" };
            return this.SelectEmailView(view)("~/src/Views/Mailr/Messages/Test.cshtml", email.Body);
        }
    }

    //[ApiVersion("4.0")]
    //[Route("api/mailr/messages")]
    //[Extension]
    //public class Message2Controller : Controller
    //{
    //    [HttpPost("[action]")]
    //    [LogResponseBody]
    //    [ServiceFilter(typeof(ValidateModel))]
    //    [ServiceFilter(typeof(SendEmail))]
    //    public IActionResult Test([FromBody] Email<TestBody> email)
    //    {
    //        email.Body = new TestBody { Greeting = "Version 4.0" };
    //        return this.EmailView(EmailView.Original)("~/src/Views/Mailr/Messages/Test.cshtml", email.Body);
    //    }
    //}    
}
