using System;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Mirality.WatchableValue
{
    /// <summary>A value, and a token that indicates that the value is no longer valid.</summary>
    /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
    public readonly struct WatchableValue<T>
    {
        /// <summary>Construct a new watchable value.</summary>
        /// <param name="value">The watched value.</param>
        /// <param name="watchToken">A token that indicates when the <paramref name="value"/> is no longer valid.</param>
        public WatchableValue(T value, IChangeToken watchToken)
        {
            Value = value;
            WatchToken = watchToken;
        }

        /// <summary>Deconstruct a watched value.</summary>
        /// <param name="value">The watched value.</param>
        /// <param name="watchToken">A token that indicates when the <paramref name="value"/> is no longer valid.</param>
        public void Deconstruct(out T value, out IChangeToken watchToken)
        {
            value = Value;
            watchToken = WatchToken;
        }

        /// <summary>The watched value.</summary>
        public T Value { get; }

        /// <summary>A token that indicates when the <see cref="Value"/> is no longer valid.</summary>
        public IChangeToken WatchToken { get; }

        /// <summary>Projects this <see cref="Value"/> to a sub-value with the same <see cref="WatchToken"/>.</summary>
        /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
        /// <param name="selector">The projection function, similar to
        /// <see cref="Enumerable.Select{TSource,TResult}(System.Collections.Generic.IEnumerable{TSource},System.Func{TSource,TResult})">LINQ's Select</see>.</param>
        /// <returns>A <see cref="WatchableValue{TResult}"/> with the projected value and the same token.</returns>
        public WatchableValue<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            return WatchableValue.Create(selector(Value), WatchToken);
        }
    }

    /// <summary>Helper methods for <see cref="WatchableValue{T}"/>.</summary>
    public static class WatchableValue
    {
        /// <summary>Creates a <see cref="WatchableValue{T}"/>.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="value">The watched value.</param>
        /// <param name="watchToken">A token that indicates when the <paramref name="value"/> is no longer valid.</param>
        /// <returns>The new watchable value.</returns>
        public static WatchableValue<T> Create<T>(T value, IChangeToken watchToken)
        {
            return new WatchableValue<T>(value, watchToken);
        }

        /// <summary>Creates a <see cref="WatchableValue{T}"/> with a <see cref="NamedChangeToken"/>.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="value">The watched value.</param>
        /// <param name="name">A name to give this <paramref name="watchToken"/> for debugging purposes.</param>
        /// <param name="watchToken">A token that indicates when the <paramref name="value"/> is no longer valid.</param>
        /// <returns>The new watchable value.</returns>
        public static WatchableValue<T> Create<T>(T value, string name, IChangeToken watchToken)
        {
            return new WatchableValue<T>(value, new NamedChangeToken(name, watchToken));
        }

        /// <summary>Calls the given action whenever a <see cref="WatchableValue{T}"/> becomes outdated.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="producer">Produces the watchable value.</param>
        /// <param name="action">Called with the initial value, and again whenever it changes.</param>
        /// <returns>Dispose this when no longer interested.</returns>
        public static IDisposable Watch<T>(Func<WatchableValue<T>> producer, Action<WatchableValue<T>> action)
        {
            return Watch(producer, (act, value) => act!(value), action);
        }

        /// <summary>Calls the given action whenever a <see cref="WatchableValue{T}"/> becomes outdated.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <typeparam name="TState">The type of <paramref name="state"/> parameter.</typeparam>
        /// <param name="producer">Produces the watchable value.  Called to get the next value whenever it changes.</param>
        /// <param name="action">Called with the initial value, and again whenever it changes.</param>
        /// <param name="state">Additional state to pass to <paramref name="action"/>.</param>
        /// <returns>Dispose this when no longer interested.</returns>
        public static IDisposable Watch<T, TState>(Func<WatchableValue<T>> producer, Action<TState?, WatchableValue<T>> action, TState? state)
        {
            IChangeToken TokenProducer()
            {
                var value = producer();
                action(state, value);
                return value.WatchToken;
            }

            return ChangeToken.OnChange(TokenProducer, () => { });
        }
    }
}
