![.NET Standard 2.0](https://img.shields.io/static/v1?label=.NET&message=Std2.0&color=blue) [![NuGet version (Mirality.WatchableValue)](https://img.shields.io/nuget/v/Mirality.WatchableValue.svg?logo=nuget)](https://www.nuget.org/packages/Mirality.WatchableValue/)

This is a tiny library that implements a helpful vocabulary type `WatchableValue<T>`, which wraps together a value (or reference, nullable or otherwise) together with an [`IChangeToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.primitives.ichangetoken) that indicates when the value is no longer valid and may need to be reloaded or refreshed.

Note that this library is not really intended to be used with configuration values in particular (although it can be); for these, you should prefer using the [Options classes](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) instead.

Not only does it seem natural to store a value and its expiration notifier together, one of the intended advantages of producing the value and the token at the same time is where the calculation process is complex or interdependent -- for example, when producing an object model from a file that can include other files, you can produce a list of dependencies while parsing the primary file, and then a change token that triggers when any of these dependencies (or the primary file) are changed along with the single combined result.  This is more efficient than having to parse once to generate the value and again to watch for further changes, or reloading whenever anything in a directory changes (which might be some unrelated files, and/or miss files included from other directories).

It does assume that you will be watching all the time; if you're only sometimes watching for changes (e.g. only in development environment) then this may not be suitable, although you can use tricks such as swapping out your token generator to only return a `NullChangeToken` in cases where you want to disable the watching.

It is compiled for .NET Standard 2.0, so it can be used from .NET Framework 4.7.2+ (assuming that you have a suitable file provider) as well as .NET Core/5+.  As of this writing, it is primarily tested with .NET 6.

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

Also included is an extension method for `IFileProvider` which simply wraps file info together with a change token:

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

### `NamedChangeToken`

One downside of the standard change tokens is that they don't carry any particularly useful information for debugging or logging what each token was actually watching.  (This isn't a bad default for a core component, since it reduces memory requirements, but it can be inconvenient.)  This library also introduces another thin wrapper, `NamedChangeToken`, which simply wraps an arbitrary string identifier around any other change token:

```cs
return new NamedChangeToken(name, token);
```

The `WatchFileInfo` method uses this to preserve the `subpath` being watched.

For example, this can be used to inspect a `CompositeChangeToken` in the debugger to see the entire list of files being watched -- or theoretically to log which particular file triggered the overall notification (though that's trickier than it sounds, and may be better handled a different way).  This can of course be used for non-file tokens as well.

# Notes

The watched value could be as small as a single integer or string, or as large as an entire parsed object model, or anywhere in between.

The change token does not have to be unique to a particular value -- it's entirely valid for e.g. a data file manager to hand out the same token (for a file as a whole) along with values derived from the contents of the file.  And of course any change token can be used, including a [`CompositeChangeToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.primitives.compositechangetoken) from multiple files or other sources.  You can also use a separate token internally from the one you expose to your consumers.  However in practice if you want to do this sort of thing you should be careful to refresh your internal token and reload appropriately before signalling the external token, or otherwise ensure that you only reload once per change while still delivering all the new values, especially if multiple values are being watched from the same source.

`Watch` assumes that tokens will actively invoke callbacks (`ActiveChangeCallbacks == true`) on change (it doesn't assert this, to remain compatible with `NullChangeToken`).  If you want to watch a polling-only token, then you will either have to use a different method or wrap it into a new token that uses a timer to triggers callbacks.

`WatchableValue.Watch` is a little different from `ChangeToken.OnChange` – the latter will call your action only when a change is detected, while the former will call immediately and then again on each change.  This difference is because it's more efficient when the value and token are calculated together.

Finally, note that the callbacks are delivered however the token chooses to call them.  This usually means that they will happen immediately and possibly from a background thread.  As such, you may need to marshal them to your main thread and/or debounce to wait for a series of changes to "settle" before using the new values.  Code for that is outside the scope of this library.

