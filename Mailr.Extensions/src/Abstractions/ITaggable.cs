using System.Collections.Generic;

namespace Mailr.Extensions.Abstractions
{
    public interface ITaggable
    {
        HashSet<string> Tags { get; }
    }
}