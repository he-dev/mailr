using Reusable.Quickey;

namespace Mailr.Extensions
{
    [UseMember]
    public static class Features
    {
        public static string SendEmail { get; } = Selector.For(() => SendEmail).ToString();
    }
}