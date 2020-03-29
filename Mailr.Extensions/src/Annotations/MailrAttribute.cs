using Reusable.Utilities.JsonNet.Annotations;

namespace Mailr.Extensions.Annotations
{
    public class MailrAttribute : NamespaceAttribute
    {
        public MailrAttribute() : base("Mailr")
        {
            Alias = "M";
        }
    }
}