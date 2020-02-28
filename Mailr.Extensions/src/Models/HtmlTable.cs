using System.Collections.Generic;
using JetBrains.Annotations;
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

    public class HtmlTableCell
    {
        public object? Value { get; set; }

        public ISet<string>? Styles { get; set; } = new HashSet<string>(SoftString.Comparer);

        public ISet<string>? Tags { get; set; } = new HashSet<string>(SoftString.Comparer);
    }
}