using System.Collections.Generic;
using System.Linq;
using Mailr.Extensions.Abstractions;

namespace Mailr.Extensions.Helpers
{
    public static class ModelHelper
    {
        public static IEnumerable<string> ToStyles(this ITaggable taggable)
        {
            return taggable.Tags.Select(GetStyle);
        }

        private static string GetStyle(string tag)
        {
            return tag.ToLower() switch
            {
                "level-error" => "level-error",
                _ => string.Empty
            };
        }
    }
}