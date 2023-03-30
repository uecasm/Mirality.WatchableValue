using System;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Mirality.WatchableValue
{
    /// <summary>This allows the value producer to use an <see cref="IProgress{T}"/> while the
    /// value consumer uses a <see cref="WatchableValue{T}"/>.</summary>
    /// <remarks>Since this uses <see cref="Progress{T}"/> under the hood, it should be
    /// constructed inside the <see cref="SynchronizationContext"/> where you want the change
    /// callbacks to execute.</remarks>
    /// <typeparam name="T">The value type.</typeparam>
    public class WatchableValueProgress<T>
    {
        private WatchableValueSource<T> _Source;

        /// <summary>Construct with an initial value.</summary>
        /// <param name="initialValue">The initial value.</param>
        /// <param name="name">An optional name to associate with the <see cref="IChangeToken"/> for debugging purposes.</param>
        public WatchableValueProgress(T initialValue, string? name = null)
        {
            _Source = new WatchableValueSource<T>(initialValue);

            Progress = new Progress<T>(value => WatchableValueSource.Change(ref _Source, value, name));
        }

        /// <summary>The <see cref="IProgress{T}"/> interface, used to produce new values.</summary>
        public IProgress<T> Progress { get; }

        /// <summary>The latest <see cref="WatchableValue{T}"/> produced.</summary>
        /// <remarks>Use <see cref="WatchableValue.Watch{T}"/> to subscribe to changes, or just read as needed.</remarks>
        public WatchableValue<T> Value => _Source.Value;
    }
}
