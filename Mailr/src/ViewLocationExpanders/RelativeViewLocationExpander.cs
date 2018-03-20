﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Mailr.ViewLocationExpanders
{
    public class RelativeViewLocationExpander : IViewLocationExpander
    {
        private readonly string _prefix;

        public RelativeViewLocationExpander(string prefix)
        {
            _prefix = prefix;
        }

        public void PopulateValues(ViewLocationExpanderContext context)
        {
            context.Values[nameof(RelativeViewLocationExpander)] = nameof(RelativeViewLocationExpander);
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            foreach (var viewLocation in viewLocations)
            {
                yield return $"/{_prefix}{viewLocation}";
            }
        }
    }
}