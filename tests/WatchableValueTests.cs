namespace Mirality.WatchableValue.Tests;

public class WatchableValueTests
{
    private record TestValue(string Part1, string? Part2);

    [Test]
    public void TestWatchToken()
    {
        var source = new CancellationTokenSource();
        var value = WatchableValue.Create("test", new CancellationChangeToken(source.Token));

        Assert.That(value.WatchToken.HasChanged, Is.False);
        source.Cancel();
        Assert.That(value.WatchToken.HasChanged, Is.True);

        var (v, t) = value;
        Assert.That(v, Is.SameAs(value.Value));
        Assert.That(t, Is.SameAs(value.WatchToken));
    }

    [Test]
    public void TestDeconstruct()
    {
        var source = new CancellationTokenSource();
        var value = WatchableValue.Create("test", "test", new CancellationChangeToken(source.Token));

        var (v, t) = value;
        Assert.Multiple(() =>
        {
            Assert.That(v, Is.SameAs(value.Value));
            Assert.That(t, Is.SameAs(value.WatchToken).And.InstanceOf<NamedChangeToken>());
            Assert.That(t.ToString(), Is.EqualTo("ChangeToken{test}"));
            Assert.That(t.ActiveChangeCallbacks, Is.True);
        });

        var (n, ct) = (NamedChangeToken) value.WatchToken;
        Assert.Multiple(() =>
        {
            Assert.That(n, Is.EqualTo("test"));
            Assert.That(ct, Is.InstanceOf<CancellationChangeToken>());
        });
    }

    [Test]
    public void TestSelect()
    {
        var source = new CancellationTokenSource();
        var token = new CancellationChangeToken(source.Token);

        var value = WatchableValue.Create(new TestValue("hello", "world"), token);

        var part1 = value.Select(v => v.Part1);
        var part2 = value.Select(v => v.Part2);

        Assert.Multiple(() =>
        {
            Assert.That(part1.Value, Is.EqualTo("hello"));
            Assert.That(part2.Value, Is.EqualTo("world"));
            Assert.That(token, Is.SameAs(value.WatchToken).And.SameAs(part1.WatchToken).And.SameAs(part2.WatchToken));
        });
    }

    [Test]
    public void TestWatch()
    {
        var sequence = new[] { "abc", "123", "test", "hello world" };
        var nextIndex = 0;
        var results = new List<string>();
        CancellationTokenSource? currentSource = null;
        NamedChangeToken? currentToken = null;

        WatchableValue<string> Producer()
        {
            currentSource = new CancellationTokenSource();

            var value = sequence[nextIndex];
            var token = new NamedChangeToken(nextIndex.ToString(), new CancellationChangeToken(currentSource.Token));
            ++nextIndex;

            return WatchableValue.Create(value, token);
        }

        void Consumer(WatchableValue<string> value)
        {
            results.Add(value.Value);
            currentToken = (NamedChangeToken) value.WatchToken;
        }

        using var subscription = WatchableValue.Watch(Producer, Consumer);

        Assert.That(results, Is.EqualTo(new[] { "abc" }));
        Assert.That(currentToken?.Name, Is.Not.Null.And.EqualTo("0"));

        currentSource!.Cancel();

        Assert.That(results, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(currentToken?.Name, Is.Not.Null.And.EqualTo("1"));

        subscription.Dispose();
        currentSource!.Cancel();

        Assert.That(results, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(currentToken?.Name, Is.Not.Null.And.EqualTo("1"));

        ++nextIndex;
        using var subscription2 = WatchableValue.Watch(Producer, Consumer);

        Assert.That(results, Is.EqualTo(new[] { "abc", "123", "hello world" }));
        Assert.That(currentToken?.Name, Is.Not.Null.And.EqualTo("3"));

        Assert.That(() => currentSource!.Cancel(), Throws.InnerException.InstanceOf<IndexOutOfRangeException>());
    }
}