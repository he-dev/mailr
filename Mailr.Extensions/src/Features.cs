using Reusable.Quickey;

namespace Mailr.Extensions
{
    [UseType, UseMember]
    [PlainSelectorFormatter]
    public class Features : SelectorBuilder<Features>
    {
        public static Selector<object> SendEmail { get; } = Select(() => SendEmail);
    }
}