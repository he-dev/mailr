using System.Collections.Generic;
using Mailr.Extensions.Abstractions;
using Reusable.Quickey;

namespace Mailr.Extensions.Utilities
{
    [UseMember]
    [PlainSelectorFormatter]
    public class HttpContextItems : SelectorBuilder<HttpContextItems>
    {
        public static Selector<IEmail> Email { get; } = Select(() => Email);

        public static Selector<string> EmailTheme { get; } = Select(() => EmailTheme);
    }

    public static class DictionaryExtension
    {
        public static TValue GetItem<TValue>(this IDictionary<object, object> dictionary, Selector<TValue> selector)
        {
            return (TValue)dictionary[selector.ToString()];
        }

        public static bool TryGetItem<TValue>(this IDictionary<object, object> dictionary, Selector<TValue> selector, out TValue item)
        {
            if (dictionary.TryGetValue(selector.ToString(), out var value) && value is TValue x)
            {
                item = x;
                return true;
            }

            item = default;
            return false;
        }

        public static IDictionary<object, object> SetItem<TValue>(this IDictionary<object, object> dictionary, Selector<TValue> selector, TValue value)
        {
            dictionary[selector.ToString()] = value;
            return dictionary;
        }
    }
}