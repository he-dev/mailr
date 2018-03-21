using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Mailr.Mvc.Razor.ViewLocationExpanders
{
    public class ExtensionViewLocationExpander : IViewLocationExpander
    {
        private readonly IEnumerable<string> _extensionNames;

        public ExtensionViewLocationExpander(IEnumerable<string> extensionNames)
        {
            _extensionNames = extensionNames;
        }


        public void PopulateValues(ViewLocationExpanderContext context)
        {
            context.Values[nameof(ExtensionViewLocationExpander)] = nameof(ExtensionViewLocationExpander);
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            // yield other view locations unchanged
            foreach (var viewLocation in viewLocations)
            {
                yield return viewLocation;
                foreach (var extensionName in _extensionNames)
                {
                    yield return $"/{extensionName}{viewLocation}";
                }
            }
        }
    }
}