![.NET Standard 2.0](https://img.shields.io/static/v1?label=.NET&message=Std2.0&color=blue) [![NuGet version (Mirality.WatchableValue)](https://img.shields.io/nuget/v/Mirality.WatchableValue.svg?logo=nuget)](https://www.nuget.org/packages/Mirality.WatchableValue/)

This is a tiny library that implements a helpful vocabulary type `WatchableValue<T>`, which wraps together a value (or reference, nullable or otherwise) together with an [`IChangeToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.primitives.ichangetoken) that indicates when the value is no longer valid and may need to be reloaded or refreshed.

Note that this library is not really intended to be used with configuration values in particular (although it can be); for those, you should prefer using the [Options classes](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) instead.

Not only does it seem natural to store a value and its expiration notifier together, one of the intended advantages of producing the value and the token at the same time is where the calculation process is complex or interdependent -- for example, when producing an object model from a file that can include other files, you can produce a list of dependencies while parsing the primary file, and then a change token that triggers when any of these dependencies (or the primary file) are changed along with the single combined result.  This is more efficient than having to parse once to generate the value and again to watch for further changes, or reloading whenever anything in a directory changes (which might be some unrelated files, and/or miss files included from other directories).

It does assume that you will be watching all the time; if you're only sometimes watching for changes (e.g. only in development environment) then this may not be suitable, although you can use tricks such as swapping out your token generator to only return a `NullChangeToken` in cases where you want to disable the watching.

It is compiled for .NET Standard 2.0, so it can be used from .NET Framework 4.7.2+ as well as .NET Core/5+.  As of this writing, it is primarily tested with .NET 6.

# Installation

Simply add the NuGet package as usual.

In some cases you may get compiler warnings about package version conflicts for some `Microsoft.Extensions.*` packages.  The best known way to resolve these is to add explicit package references to your application project for whichever version you want to actually include (typically the highest).

# Usage

All classes and methods have XML documentation, so your IDE can give you more specific reference docs.

The main intended use is as a return value from methods:

```cs
public WatchableValue<Foo> GetCurrentFoo()
{
    var foo = /* calculate the Foo */;
    var token = /* create a change token for that Foo */;
    return WatchableValue.Create(foo, token);
}
```

One way to consume this is to:

```cs
var subscription = WatchableValue.Watch(() => myFooService.GetCurrentFoo(), foo => DoSomething(foo));
// ...
subscription.Dispose();
```

Also included is an extension method for `IFileProvider` which simply wraps file info together with a change token (although this must be a specific file, not a wildcard):

```cs
var subscription = WatchableValue.Watch(() => fileProvider.WatchFileInfo("path/to/file"), file =>
{
    // called whenever the file is created/modified/deleted
    if (file.Exists) { /* ... */ }
});
// ...
subscription.Dispose();
```

You can also use this as a basis to parse a file and return an object model along with the file's token:

```cs
public WatchableValue<Foo?> LoadAndWatchFoo(string subpath)
{
    var file = _Host.WebRootFileProvider.WatchFileInfo(subpath);
    if (file.Exists)
    {
        using var stream = file.CreateReadStream();
        var foo = JsonSerializer.Deserialize<Foo>(stream);  // whatever
        return WatchableValue.Create(foo, file.WatchToken);
    }

    // yes, you can return a watch token when the file doesn't
    // exist too, which will notify if someone creates it.
    return WatchableValue.Create(null, file.WatchToken);
}
```

Given a watchable value for an object model, you can extract component or calculated values that have the same change token, in a way that should be familiar:

```cs
var part1 = value.Select(v => v.Part1);
var part2 = value.Select(v => v.Part2);
var text = value.Select(v => v.ToString());
```

## `NamedChangeToken`

One downside of the standard change tokens is that they don't carry any particularly useful information for debugging or logging what each token was actually watching.  (This isn't a bad default for a core component, since it reduces memory requirements, but it can be inconvenient.)  This library also introduces another thin wrapper, `NamedChangeToken`, which simply wraps an arbitrary string identifier around any other change token:

```cs
return new NamedChangeToken(name, token);
```

The `WatchFileInfo` method uses this to preserve the `subpath` being watched.

For example, this can be used to inspect a `CompositeChangeToken` in the debugger to see the entire list of files being watched -- or theoretically to log which particular file triggered the overall notification (though that's trickier than it sounds, and may be better handled a different way).  This can of course be used for non-file tokens as well.

## `WatchableValueSource`

If you don't already have another source of change tokens, you can use the `WatchableValueSource` as a convenience, either eagerly or lazily:

```cs
public class SomeClassThatEagerlyGeneratesFoo
{
    private WatchableValueSource<Foo> _CurrentFoo;

    public SomeClassThatEagerlyGeneratesFoo()
    {
        _CurrentFoo = WatchableValueSource.Create(GenerateNewFoo());            // initial value

        // subscribe to things that will tell you about changes
    }

    public WatchableValue<Foo> GetCurrentFoo() => _CurrentFoo.Value;
    // or as a property:
    public WatchableValue<Foo> CurrentFoo => _CurrentFoo.Value;

    // called when some internal change tokens are triggered, or another event
    private void SomeInternalEventThatChangesFoo()
    {
        _ = WatchableValueSource.Change(ref _CurrentFoo, GenerateNewFoo());     // new value
    }

    private Foo GenerateNewFoo()
    {
        // value calculations
        return new Foo { ... };
    }
}
```

```cs
public class SomeClassThatLazilyGeneratesFoo
{
    private WatchableValueSource<Foo>? _CurrentFoo;

    public SomeClassThatLazilyGeneratesFoo()
    {
        // subscribe to things that will tell you about changes
    }

    public WatchableValue<Foo> GetCurrentFoo()
    {
        return WatchableValueSource.GetOrChange(ref _CurrentFoo, GenerateNewFoo);
    }

    private void SomeInternalEventTriggeredWhenYouKnowFooIsOutdated()
    {
        _CurrentFoo?.Invalidate();
    }

    private Foo GenerateNewFoo()
    {
        // value calculations
        return new Foo { ... };
    }
}
```

The eager variant will always generate a new `Foo` even if nothing is currently subscribed; the lazy variant will wait until someone calls `GetCurrentFoo()`, either directly or from a subscription.

For best results, you should *never* access `_CurrentFoo` in any way other than as shown above -- and in particular, don't call `GetCurrentFoo()` yourself from the same code flow that calls `Change`; use the return value of `Change` instead if needed (or use local variables for `Foo` or parts thereof, if you don't need the resulting watchable value or change token).

This implements some thread-safety (atomicity) via `Interlocked.Exchange` and immutable classes, such that even if multiple threads are triggering `Change`, any observers will see consistent results from `GetCurrentFoo()`'s value and change token.  It does not require additional locking, although you may require locks for other reasons (e.g. during the calculation of the new value, or to throttle recalculations triggered by internal change tokens or events).  By itself, it does not guarantee that `GenerateNewFoo()` will not be called concurrently, just that only one of the results will be "kept" and it will present a consistent view to consumers.

You can, of course, also supply a name to go with this for debugging purposes.

## `WatchableValueProgress`

If you have some existing async APIs that produce progress values, they're probably already using `IProgress<T>`.  Or even if you're writing new code, this is a convenient interface for providing progress notifications, and also provides some integrated thread-marshaling via `SynchronizationContext`.

The `WatchableValueProgress<T>` class provides an adapter that wraps a `WatchableValueSource<T>`, such that consumers see a `WatchableValue<T>` and producers see an `IProgress<T>`:

```cs
var watchableProgress = new WatchableValueProgress<string?>(null);
using var subscription = WatchableValue.Watch(() => watchableProgress.Value, value => { ... });
await SomeMethod(watchableProgress.Progress, ...);
```

This also supports providing a name for debugging purposes, although only for the progress object as a whole, not editable with each new value.

# Notes

The watched value could be as small as a single integer or string, or as large as an entire parsed object model, or anywhere in between.

The change token does not have to be unique to a particular value -- it's entirely valid for e.g. a data file manager to hand out the same token (for a file as a whole) along with values derived from the contents of the file.  And of course any change token can be used, including a [`CompositeChangeToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.primitives.compositechangetoken) from multiple files or other sources.  You can also use a separate token internally from the one you expose to your consumers.  However in practice if you want to do this sort of thing you should be careful to refresh your internal token and reload appropriately before signalling the external token, or otherwise ensure that you only reload once per change while still delivering all the new values, especially if multiple values are being watched from the same source.

When the change token signals a change, that does not necessarily mean that the associated value *actually* changed -- it simply means that the source of the value now considers it potentially stale, or it has generated a new instance for reasons of its own.  For example, a file could have been re-written with exactly the same content, or with a change to content that did not affect the resulting value, or that affected some part of a larger value but not the particular `Select`ed subset you're currently using.  You may still need to keep track of the previous value and check if it actually changed, if processing the change is expensive or has side effects.

`Watch` assumes that tokens will actively invoke callbacks (`ActiveChangeCallbacks == true`) on change (it doesn't assert this, to remain compatible with `NullChangeToken`).  If you want to watch a polling-only token, then you will either have to use a different method or wrap it into a new token that uses a timer to triggers callbacks.  Having said that, at the end-use you don't *have* to actively watch/subscribe for changes, you can lazily poll (e.g. on user action, not a timer) if that makes more sense for your usage.  But intermediate token providers should support both styles.

`WatchableValue.Watch` is a little different from `ChangeToken.OnChange` – the latter will call your action only when a change is detected, while the former will call immediately and then again on each change.  This difference is because it's more efficient when the value and token are calculated together.

Finally, note that the callbacks are delivered however the token chooses to call them.  This usually means that they will happen immediately and possibly from a background thread.  As such, you may need to marshal them to your main thread and/or debounce to wait for a series of changes to "settle" before using the new values.  Code for that is outside the scope of this library.
