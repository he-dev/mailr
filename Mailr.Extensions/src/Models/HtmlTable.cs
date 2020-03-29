using System.Collections.Generic;
using JetBrains.Annotations;
using Mailr.Extensions.Abstractions;
using Reusable;

namespace Mailr.Extensions.Models
{
    [UsedImplicitly]
    public class HtmlTable
    {
        public HtmlTableRowGroup? Head { get; set; }

        public HtmlTableRowGroup? Body { get; set; }

        public HtmlTableRowGroup? Foot { get; set; }
    }

    public class HtmlTableRow : List<HtmlTableCell>
    {
        public static HtmlTableRow Empty => new HtmlTableRow();
    }

    public class HtmlTableRowGroup : List<HtmlTableRow>
    {
        public static HtmlTableRowGroup Empty => new HtmlTableRowGroup();
    }

    public class HtmlTableCell : ITaggable
    {
        public object? Value { get; set; }

        public HashSet<string>? Styles { get; set; } = new HashSet<string>(SoftString.Comparer);

        public HashSet<string>? Tags { get; set; } = new HashSet<string>(SoftString.Comparer);
    }
}