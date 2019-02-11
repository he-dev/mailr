using System.Collections.Generic;
using JetBrains.Annotations;

namespace Mailr.Extensions.Models
{
    public class HtmlTable
    {
        [CanBeNull]
        public HtmlTableRowGroup Head { get; set; }

        [CanBeNull]
        public HtmlTableRowGroup Body { get; set; }

        [CanBeNull]
        public HtmlTableRowGroup Foot { get; set; }
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
        [CanBeNull]
        public IList<string> Styles { get; set; }

        [CanBeNull]
        public object Value { get; set; }
    }
}