using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Mailr.ViewLocationExpanders
{
    public class EmailViewLocationExpander : IViewLocationExpander
    {
        private readonly string _prefix;

        public EmailViewLocationExpander(string prefix)
        {
            _prefix = prefix;
        }

        public void PopulateValues(ViewLocationExpanderContext context)
        {
            context.Values[nameof(EmailViewLocationExpander)] = nameof(EmailViewLocationExpander);
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            // yield other view locations unchanged
            foreach (var viewLocation in viewLocations)
            {
                yield return viewLocation;
            }

            yield return $"/{_prefix}/Views/Emails/{{1}}/{{0}}{RazorViewEngine.ViewExtension}";
        }
    }
}