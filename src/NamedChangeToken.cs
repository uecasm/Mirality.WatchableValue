using System;
using Microsoft.Extensions.Primitives;

namespace Mirality.WatchableValue
{
    /// <summary>This is a wrapper for <see cref="IChangeToken"/> that embeds a name, which can be useful for debugging/logging purposes.</summary>
    /// <param name="Name">The name of this token.</param>
    /// <param name="ChildToken">The child token being wrapped.</param>
    public record NamedChangeToken(string Name, IChangeToken ChildToken) : IChangeToken
    {
        /// <inheritdoc />
        public override string ToString()
        {
            return $"ChangeToken{{{Name}}}";
        }

        /// <inheritdoc />
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return ChildToken.RegisterChangeCallback(callback, state);
        }

        /// <inheritdoc />
        public bool HasChanged => ChildToken.HasChanged;

        /// <inheritdoc />
        public bool ActiveChangeCallbacks => ChildToken.ActiveChangeCallbacks;
    }
}
