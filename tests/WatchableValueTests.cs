namespace Mirality.WatchableValue.Tests;

public class WatchableValueTests
{
    private record TestValue(string Part1, string? Part2);

    [Test]
    public void TestWatchTokenViaExternal()
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
    public void TestWatchTokenViaSource()
    {
        var source = WatchableValueSource.Create("test");

        Assert.That(source.Value.WatchToken.HasChanged, Is.False);
        source.Invalidate();
        Assert.That(source.Value.WatchToken.HasChanged, Is.True);

        var (v, t) = source.Value;
        Assert.That(v, Is.SameAs(source.Value.Value));
        Assert.That(t, Is.SameAs(source.Value.WatchToken));
    }

    [Test]
    public void TestDeconstruct()
    {
        var source = WatchableValueSource.Create("test", "test");

        var (v, t) = source.Value;
        Assert.Multiple(() =>
        {
            Assert.That(v, Is.SameAs(source.Value.Value));
            Assert.That(t, Is.SameAs(source.Value.WatchToken).And.InstanceOf<NamedChangeToken>());
            Assert.That(t.ToString(), Is.EqualTo("ChangeToken{test}"));
            Assert.That(t.ActiveChangeCallbacks, Is.True);
        });

        var (n, ct) = (NamedChangeToken) source.Value.WatchToken;
        Assert.Multiple(() =>
        {
            Assert.That(n, Is.EqualTo("test"));
            Assert.That(ct, Is.InstanceOf<CancellationChangeToken>());
        });
    }

    [Test]
    public void TestSelect()
    {
        var source = WatchableValueSource.Create(new TestValue("hello", "world"));

        var part1 = source.Value.Select(v => v.Part1);
        var part2 = source.Value.Select(v => v.Part2);

        Assert.Multiple(() =>
        {
            Assert.That(part1.Value, Is.EqualTo("hello"));
            Assert.That(part2.Value, Is.EqualTo("world"));
            Assert.That(source.Value.WatchToken, Is.SameAs(part1.WatchToken).And.SameAs(part2.WatchToken));
        });
    }

    [Test]
    public void TestWatchEager()
    {
        var consumer = new ValueConsumer<string>();
        var producer = new ValueProducer<string>("abc", "0");
        using var subscription = WatchableValue.Watch(() => producer.Value, consumer.Consume);

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("0"));

        var another = producer.Value;
        Assert.That(another.Value, Is.EqualTo("abc"));
        Assert.That(another.WatchToken, Is.SameAs(consumer.LastChangeToken));

        producer.Change("123", "1");

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("1"));

        subscription.Dispose();
        producer.Change("test", "2");

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("1"));

        producer.Change("hello world", "3");
        using var subscription2 = WatchableValue.Watch(() => producer.Value, consumer.Consume);

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc", "123", "hello world" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("3"));
    }

    [Test]
    public void TestWatchLazy()
    {
        var consumer = new ValueConsumer<string>();
        var producer = new LazyValueProducer<string>(new[] { "abc", "123", "test", "hello world" });

        using var subscription = WatchableValue.Watch(() => producer.GetCurrentValue(), consumer.Consume);

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("0"));

        var another = producer.GetCurrentValue();
        Assert.That(another.Value, Is.EqualTo("abc"));
        Assert.That(another.WatchToken, Is.SameAs(consumer.LastChangeToken));

        producer.Invalidate();

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("1"));

        subscription.Dispose();
        producer.Invalidate();

        Assert.That(consumer.Values, Is.EqualTo(new[] { "abc", "123" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("1"));

        var newValue = producer.GetCurrentValue();

        Assert.That(newValue.Value, Is.EqualTo("test"));
        Assert.That(((NamedChangeToken) newValue.WatchToken).Name, Is.EqualTo("2"));

        var newValue2 = producer.GetCurrentValue();

        Assert.That(newValue2.Value, Is.EqualTo("test"));
        Assert.That(newValue2.WatchToken, Is.SameAs(newValue.WatchToken));
    }

    [Test]
    public void TestNullable()
    {
        var consumer = new ValueConsumer<string?>();

        WatchableValueSource<string?>? source = null;

        WatchableValueSource.Change(ref source, "test", "0");

        // ReSharper disable once AccessToModifiedClosure
        using var subscription = WatchableValue.Watch(() => source.Value, consumer.Consume);

        Assert.That(consumer.Values, Is.EqualTo(new[] { "test" }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("0"));

        WatchableValueSource.Change(ref source, null, "1");

        Assert.That(consumer.Values, Is.EqualTo(new[] { "test", null }));
        Assert.That(consumer.LastChangeToken?.Name, Is.Not.Null.And.EqualTo("1"));
    }

    private class ValueProducer<T>
    {
        private WatchableValueSource<T> _CurrentValue;

        public ValueProducer(T initialValue, string name)
        {
            _CurrentValue = WatchableValueSource.Create(initialValue, name);
        }

        public WatchableValue<T> Value => _CurrentValue.Value;

        public void Change(T newValue, string name)
        {
            _ = WatchableValueSource.Change(ref _CurrentValue, newValue, name);
        }
    }

    private class LazyValueProducer<T>
    {
        private readonly IEnumerator<T> _NextValue;
        private int _CurrentIndex = 0;
        private WatchableValueSource<T>? _CurrentValue;

        public LazyValueProducer(IEnumerable<T> values)
        {
            _NextValue = values.GetEnumerator();
            // ignoring _NextValue.Dispose() since this is a no-op for array/list
        }

        public void Invalidate()
        {
            _CurrentValue?.Invalidate();
        }

        public WatchableValue<T> GetCurrentValue()
        {
            return WatchableValueSource.GetOrChange(ref _CurrentValue, GetNextValue, _CurrentIndex.ToString());
        }

        private T GetNextValue()
        {
            if (_NextValue.MoveNext())
            {
                ++_CurrentIndex;
                return _NextValue.Current;
            }

            throw new IndexOutOfRangeException();
        }
    }

    private class ValueConsumer<T>
    {
        public List<T> Values { get; } = new();
        public NamedChangeToken? LastChangeToken { get; private set; }

        public void Consume(WatchableValue<T> value)
        {
            Values.Add(value.Value);
            LastChangeToken = (NamedChangeToken) value.WatchToken;
        }
    }
}