namespace Mirality.WatchableValue.Tests;

public class FileProviderTests
{
    private string _TestDirectory;
    private IFileProvider _FileProvider;
    private TimeSpan _WatchTimeout;

    [SetUp]
    public void SetUp()
    {
        //Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");

        _TestDirectory = Path.Combine(Path.GetTempPath(), "Mirality.WatchableValue");

        if (Directory.Exists(_TestDirectory))
        {
            Directory.Delete(_TestDirectory, true);
        }
        Directory.CreateDirectory(_TestDirectory);

        _FileProvider = new PhysicalFileProvider(_TestDirectory);
        _WatchTimeout = ((PhysicalFileProvider) _FileProvider).UsePollingFileWatcher ? TimeSpan.FromSeconds(4.1) : TimeSpan.FromMilliseconds(100);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_TestDirectory))
        {
            Directory.Delete(_TestDirectory, true);
        }
    }

    private static string? ReadFile(IFileInfo file)
    {
        if (!file.Exists) { return null; }

        using var stream = new StreamReader(file.CreateReadStream());
        return stream.ReadToEnd();
    }

    private static bool WaitUntil(TimeSpan timeout, [InstantHandle] Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            Thread.Sleep(1);

            if (condition())
            {
                return true;
            }
        }

        return false;
    }

    [Test, Order(1)]
    public void NonPolling()
    {
        if (_WatchTimeout >= TimeSpan.FromSeconds(1))
        {
            Assert.Warn("Tests may run very slowly; file system watcher is using polling.");
        }
    }

    [Test]
    public void Modifications()
    {
        File.WriteAllText(Path.Combine(_TestDirectory, "one.txt"), "this is a test");

        var watch = _FileProvider.WatchFileInfo("one.txt");
        Assert.Multiple(() =>
        {
            Assert.That(watch.Value.Exists, Is.True);
            Assert.That(watch.WatchToken.HasChanged, Is.False);
            Assert.That(ReadFile(watch.Value), Is.EqualTo("this is a test"));
        });

        File.WriteAllText(Path.Combine(_TestDirectory, "one.txt"), "this is another test");
        Assert.That(WaitUntil(_WatchTimeout, () => watch.WatchToken.HasChanged));

        watch = _FileProvider.WatchFileInfo("one.txt");
        Assert.Multiple(() =>
        {
            Assert.That(watch.Value.Exists, Is.True);
            Assert.That(watch.WatchToken.HasChanged, Is.False);
            Assert.That(ReadFile(watch.Value), Is.EqualTo("this is another test"));
        });
    }

    [Test]
    public void CreateAndDelete()
    {
        var watch = _FileProvider.WatchFileInfo("test.txt");
        Assert.Multiple(() =>
        {
            Assert.That(watch.Value.Exists, Is.False);
            Assert.That(watch.WatchToken.HasChanged, Is.False);
            Assert.That(ReadFile(watch.Value), Is.Null);
        });

        File.WriteAllText(Path.Combine(_TestDirectory, "test.txt"), "this is a test");
        Assert.That(WaitUntil(_WatchTimeout, () => watch.WatchToken.HasChanged));

        watch = _FileProvider.WatchFileInfo("test.txt");
        Assert.Multiple(() =>
        {
            Assert.That(watch.Value.Exists, Is.True);
            Assert.That(watch.WatchToken.HasChanged, Is.False);
            Assert.That(ReadFile(watch.Value), Is.EqualTo("this is a test"));
        });

        File.Delete(Path.Combine(_TestDirectory, "test.txt"));
        Assert.That(WaitUntil(_WatchTimeout, () => watch.WatchToken.HasChanged));

        watch = _FileProvider.WatchFileInfo("test.txt");
        Assert.Multiple(() =>
        {
            Assert.That(watch.Value.Exists, Is.False);
            Assert.That(watch.WatchToken.HasChanged, Is.False);
            Assert.That(ReadFile(watch.Value), Is.Null);
        });
    }

    [Test]
    public void WatchCallback()
    {
        var contents = WatchableValue.Create((string?) null, NullChangeToken.Singleton);
        var called = 0;

        void Consumer(WatchableValue<IFileInfo> file)
        {
            contents = file.Select(ReadFile);
            ++called;
        }

        Assert.Multiple(() =>
        {
            Assert.That(contents.Value, Is.Null);
            Assert.That(contents.WatchToken.HasChanged, Is.False);
        });

        var subscription = WatchableValue.Watch(() => _FileProvider.WatchFileInfo("two.txt"), Consumer);
        Assert.That(WaitUntil(_WatchTimeout, () => called == 1));
        Assert.Multiple(() =>
        {
            Assert.That(contents.Value, Is.Null);
            Assert.That(contents.WatchToken.HasChanged, Is.False);
        });

        var oldContents = contents;
        var oldCalled = called;
        File.WriteAllText(Path.Combine(_TestDirectory, "two.txt"), "this is a test");
        Assert.That(WaitUntil(_WatchTimeout, () => called > oldCalled));    // this could increment by 1 or 2, due to possible separate Created vs Changed notifications
        Assert.Multiple(() =>
        {
            Assert.That(oldContents.WatchToken.HasChanged, Is.True);
            Assert.That(contents.Value, Is.EqualTo("this is a test"));
            Assert.That(contents.WatchToken.HasChanged, Is.False);
        });

        oldContents = contents;
        oldCalled = called;
        File.WriteAllText(Path.Combine(_TestDirectory, "two.txt"), "this is another test");
        Assert.That(WaitUntil(_WatchTimeout, () => called > oldCalled));
        Assert.Multiple(() =>
        {
            Assert.That(oldContents.WatchToken.HasChanged, Is.True);
            Assert.That(contents.Value, Is.EqualTo("this is another test"));
            Assert.That(contents.WatchToken.HasChanged, Is.False);
        });
    }
}
