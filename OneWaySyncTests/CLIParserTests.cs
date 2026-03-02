using OneWaySync.GlobalHelpers;

namespace OneWaySyncTests;

[TestFixture]
public class CLIParserTests
{
    private CLIParser _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new CLIParser();
    }

    [Test]
    public void Parse_ValidArgs_MapsToUserInput()
    {
        var args = new[]
        {
            @"C:\src",
            @"C:\dst",
            "60",
            @"C:\logs\app.log"
        };

        var result = _sut.Parse(args);

        Assert.That(result.SourceDirectory, Is.EqualTo(@"C:\src"));
        Assert.That(result.DestinationDirectory, Is.EqualTo(@"C:\dst"));
        Assert.That(result.SynchronizationInterval, Is.EqualTo(60));
        Assert.That(result.LogFilePath, Is.EqualTo(@"C:\logs\app.log"));
    }

    [TestCase(int.MaxValue, int.MaxValue)]
    [TestCase(int.MinValue, int.MaxValue)]
    [TestCase(1, 1)]
    [TestCase(0, 1)]
    [TestCase(-1, 1)]
    [TestCase(-999, 999)]
    [TestCase(3600, 3600)]
    public void Parse_GuardSyncInterval_BoundaryAndTypicalValues(
    int inputInterval,
    int expectedInterval)
    {
        var args = new[]
        {
        @"C:\src",
        @"C:\dst",
        inputInterval.ToString(),
        @"C:\logs\app.log"
        };

        var result = _sut.Parse(args);

        Assert.That(result.SynchronizationInterval, Is.EqualTo(expectedInterval));
    }

    [TestCaseSource(nameof(TooManyArgsCases))]
    public void Parse_FiveOrMoreArguments_ExtraValuesAreIgnored(string[] args)
    {
        var result = _sut.Parse(args);

        Assert.That(result.SourceDirectory, Is.EqualTo("a"));
        Assert.That(result.DestinationDirectory, Is.EqualTo("b"));
        Assert.That(result.SynchronizationInterval, Is.EqualTo(1));
        Assert.That(result.LogFilePath, Is.EqualTo("log.txt"));
    }
    private static readonly object[] TooManyArgsCases =
    {
        new object[] { new[] { "a", "b", "1", "log.txt", "Fifth_extra" } },
        new object[] { new[] { "a", "b", "1", "log.txt", "extra1", "Sixth_extra" } },
        new object[] { new[] { "a", "b", "1", "log.txt", "x", "y", "seventh_extra" } }
    };

[TestCaseSource(nameof(InvalidArgsCases))]
    public void Parse_InvalidArgs_ThrowsArgumentException(string[] args)
    {
        var ex = Assert.Throws<ArgumentException>(() => _sut.Parse(args));
        Assert.That(ex!.Message, Is.EqualTo("Invalid CLI arguments"));
    }

    private static readonly object[] InvalidArgsCases =
    {
        // missing arguments (CLIOptions have 4 required values 0..3)
        new object[] { new[] { @"C:\src", @"C:\dst", "60" } },
        new object[] { new[] { @"C:\src", @"C:\dst" } },
        new object[] { Array.Empty<string>() },

        // interval isn't int
        new object[] { new[] { @"C:\src", @"C:\dst", "not-an-int", @"C:\logs\app.log" } },
    };




}