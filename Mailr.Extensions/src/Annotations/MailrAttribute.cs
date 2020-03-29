using Reusable.Utilities.JsonNet.Annotations;

namespace Mailr.Extensions.Annotations
{
    public class MailrAttribute : JsonTypeSchemaAttribute
    {
        public MailrAttribute() : base("Mailr")
        {
            Alias = "M";
        }
    }
}