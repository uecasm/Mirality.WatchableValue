using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Mirality.WatchableValue
{
    /// <summary>A source of <see cref="WatchableValue{T}"/>.</summary>
    /// <remarks>Store this in a field of a class that wants to hand out watchable values.</remarks>
    public class WatchableValueSource<T>
    {
        private readonly CancellationTokenSource _TokenSource = new();

        /// <summary>Construct a value source.</summary>
        /// <param name="value">The initial value.</param>
        /// <param name="name">An optional name to associate with the <see cref="IChangeToken"/> for debugging purposes.</param>
        public WatchableValueSource(T value, string? name = null)
        {
            var token = new CancellationChangeToken(_TokenSource.Token);

            Value = name == null ? WatchableValue.Create(value, token) : WatchableValue.Create(value, name, token);
        }

        /// <summary>The watchable value.</summary>
        public WatchableValue<T> Value { get; }

        /// <summary>Mark <see cref="Value"/> as no longer valid.</summary>
        /// <remarks>Prefer using <see cref="WatchableValueSource.Change{T}(ref Mirality.WatchableValue.WatchableValueSource{T}?,T,string?)"/> over calling this directly, if the reason is that a new value is available.</remarks>
        public void Invalidate()
        {
            _TokenSource.Cancel();
        }
    }

    /// <summary>Helper methods for <see cref="WatchableValueSource{T}"/>.</summary>
    public static class WatchableValueSource
    {
        /// <summary>Creates a <see cref="WatchableValueSource{T}"/>.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="value">The initial value.</param>
        /// <param name="name">An optional name to associate with the <see cref="IChangeToken"/> for debugging purposes.</param>
        /// <returns>The new value source.</returns>
        public static WatchableValueSource<T> Create<T>(T value, string? name = null)
        {
            return new WatchableValueSource<T>(value, name);
        }

        /// <summary>Update a <see cref="WatchableValueSource{T}"/> field with a new value with thread-safety.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="field"><para>A field that stores the value source.</para><para>This is allowed to be initially null; it will always be not null after this is called.</para></param>
        /// <param name="newValue">The new actual value to report.</param>
        /// <param name="name">An optional name to associate with the <see cref="IChangeToken"/> for debugging purposes.</param>
        /// <returns>The new <see cref="WatchableValueSource{T}"/> (using this is more thread-safe than accessing <paramref name="field"/> directly).</returns>
        public static WatchableValueSource<T> Change<T>([NotNull] ref WatchableValueSource<T>? field, T newValue, string? name = null)
        {
            var newSource = Create(newValue, name);
            Change(ref field, newSource);
            return newSource;
        }

        /// <summary>Update a <see cref="WatchableValueSource{T}"/> field with a new source with thread-safety.</summary>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="field"><para>A field that stores the value source.</para><para>This is allowed to be initially null; it will always be not null after this is called.</para></param>
        /// <param name="newSource">The new value source to assign.</param>
        public static void Change<T>([NotNull] ref WatchableValueSource<T>? field, WatchableValueSource<T> newSource)
        {
            Interlocked.Exchange(ref field, newSource ?? throw new ArgumentNullException(nameof(newSource)))?.Invalidate();
#pragma warning disable CS8777  // 'field' is guaranteed not null by Exchange (since newSource is)
        }
#pragma warning restore CS8777

        /// <summary>Returns the existing value if it is still valid, or calls the given factory to calculate a new one if not.</summary>
        /// <remarks>If this is called concurrently while the value is not valid, it may call <paramref name="factory"/> concurrently as well.</remarks>
        /// <typeparam name="T">The type of value (reference or value type, nullable or not).</typeparam>
        /// <param name="field"><para>A field that stores the value source.</para><para>This is allowed to be initially null; it will always be not null after this is called.</para></param>
        /// <param name="factory">A factory method called to produce a new value if the current one is invalid.</param>
        /// <param name="name">An optional name to associate with the <see cref="IChangeToken"/> for debugging purposes.</param>
        /// <returns>The <see cref="WatchableValue{T}"/>.</returns>
        public static WatchableValue<T> GetOrChange<T>([NotNull] ref WatchableValueSource<T>? field,
            Func<T> factory, string? name = null)
        {
            var value = field?.Value;

            if (value?.WatchToken.HasChanged == false)
            {
#pragma warning disable CS8777  // can only reach here if 'field' is already not null
                return value.Value;
#pragma warning restore CS8777
            }

            return Change(ref field, factory(), name).Value;
        }
    }
}
