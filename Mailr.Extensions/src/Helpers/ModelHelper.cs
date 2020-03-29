using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Custom;
using Mailr.Extensions.Abstractions;
using Reusable.Collections.Generic;

namespace Mailr.Extensions.Helpers
{
    public static class ModelHelper
    {
        public static IEnumerable<string> Classes(this ITaggable taggable, Func<string, string> mapTagToClass)
        {
            return taggable.Tags.Select(mapTagToClass);
        }

        public static string Concat(this IEnumerable<string> values) => values.Join(" ");

        public static string ToClassString(this ITaggable taggable, Func<string, string> mapTagToClass)
        {
            return taggable.Classes(mapTagToClass).Concat();
        }
    }
}