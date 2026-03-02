using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OneWaySync.CLIParser;
using OneWaySync.GlobalHelpers;

namespace OneWaySyncTests;

[TestFixture]
public class ValidateTests
{
    [Test]
    public void Validate_HappyPath_DoesNotThrow_AndCreatesProbeFile()
    {
        var inputValidatorBuilder = new InputValidatorBuilder()
            .HappyPath();

        Assert.DoesNotThrow(() => inputValidatorBuilder.Sut.Validate(inputValidatorBuilder.ValidInput()));

        // write-check 
        inputValidatorBuilder.FileSystem.Verify(x => x.CreateNewFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
        inputValidatorBuilder.FileSystem.Verify(x => x.DeleteFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_SourceDirectoryMissing_ThrowsArgumentException(string? src)
    {
        var builder = new InputValidatorBuilder(MockBehavior.Loose);

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput(src, InputValidatorBuilder.DST)));
        Assert.That(ex!.ParamName, Is.EqualTo(nameof(UserInput.SourceDirectory)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_DestinationDirectoryMissing_ThrowsArgumentException(string? dst)
    {
        var builder = new InputValidatorBuilder(MockBehavior.Loose);

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput(InputValidatorBuilder.SRC, dst)));
        Assert.That(ex!.ParamName, Is.EqualTo(nameof(UserInput.DestinationDirectory)));
    }

    [Test]
    public void Validate_WhenDirectoriesNested_ThrowsArgumentException()
    {
        var builder = new InputValidatorBuilder()
            .WithNested();

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Nested Source and Destination directory"));
    }

    [Test]
    public void Validate_SourceDoesNotExist_ThrowsArgumentException()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithSourceExists(false);

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Source directory doesn't exist"));
    }

    [Test]
    public void Validate_DestinationMissing_CreatesDirectory_ThenContinues()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithDestinationCreatedThenExists();

        Assert.DoesNotThrow(() => builder.Sut.Validate(builder.ValidInput()));

        builder.FileSystem.Verify(x => x.CreateDirectory(InputValidatorBuilder.DST), Times.Once);
        builder.FileSystem.Verify(x => x.CreateNewFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
        builder.FileSystem.Verify(x => x.DeleteFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
    }

    [Test]
    public void Validate_DestinationMissing_AndCreateDoesNotHelp_ThrowsArgumentException()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithDestinationNeverExistsEvenAfterCreate();

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("cannot be created"));

        builder.FileSystem.Verify(x => x.CreateDirectory(InputValidatorBuilder.DST), Times.Once);
    }

    [Test]
    public void Validate_SourceNotReadable_ThrowsUnauthorizedAccessException()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithReadableThrows(InputValidatorBuilder.SRC, new UnauthorizedAccessException("Unauthorized"));

        var ex = Assert.Throws<UnauthorizedAccessException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Source directory is not readable"));
    }

    [Test]
    public void Validate_SourceInaccessible_ThrowsArgumentException_WithInnerException()
    {
        var inner = new InvalidOperationException("InvalidOperation");
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithReadableThrows(InputValidatorBuilder.SRC, inner);

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Source directory is inaccessible"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void Validate_DestinationInaccessible_ThrowsArgumentException_WithInnerException()
    {
        var inner = new Exception("DestinationInaccessible");
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithReadableThrows(InputValidatorBuilder.DST, inner);

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Destination directory is inaccessible"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void Validate_DestinationWriteUnauthorized_ThrowsUnauthorizedAccessException()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithDestinationWriteThrows(new UnauthorizedAccessException("Unauthorized"));

        var ex = Assert.Throws<UnauthorizedAccessException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("No permission for writing in destination directory"));

        builder.FileSystem.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Validate_DestinationWriteOtherError_ThrowsIOException_WithInnerException()
    {
        var inner = new InvalidOperationException("InvalidOperation");
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithDestinationWriteThrows(inner);

        var ex = Assert.Throws<IOException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("Can't write in destination directory"));
        Assert.That(ex.InnerException, Is.SameAs(inner));

        builder.FileSystem.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }
}


internal sealed class InputValidatorBuilder
{
    public Mock<IFileSystem> FileSystem { get; }
    public Mock<IPathService> PathService { get; }
    public Mock<ICLIParser> CliParser { get; }
    public InputValidator Sut { get; }

    public const string SRC = "SRC";
    public const string DST = "DST";
    public const string PROBE_NAME = "probe.tmp";
    public const string PROBE_PATH = @"DST\probe.tmp";

    public InputValidatorBuilder(MockBehavior behavior = MockBehavior.Strict)
    {
        FileSystem = new Mock<IFileSystem>(behavior);
        PathService = new Mock<IPathService>(behavior);
        CliParser = new Mock<ICLIParser>(behavior);

        Sut = new InputValidator(NullLogger.Instance, FileSystem.Object, PathService.Object, CliParser.Object);
    }

    public UserInput ValidInput(string? src = SRC, string? dst = DST)
        => new() { SourceDirectory = src!, DestinationDirectory = dst! };

    public InputValidatorBuilder HappyPath()
    {
        // path normalization + nesting
        PathService.Setup(x => x.NormalizePath(SRC)).Returns(SRC);
        PathService.Setup(x => x.NormalizePath(DST)).Returns(DST);
        PathService.Setup(x => x.DirectoriesAreNested(SRC, DST)).Returns(false);

        // existence
        FileSystem.Setup(x => x.DirectoryExists(SRC)).Returns(true);
        FileSystem.Setup(x => x.DirectoryExists(DST)).Returns(true);

        // readable checks
        FileSystem.Setup(x => x.EnumerateFileSystemEntries(SRC)).Returns(Array.Empty<string>());
        FileSystem.Setup(x => x.EnumerateFileSystemEntries(DST)).Returns(Array.Empty<string>());

        // probe file write/delete
        PathService.Setup(x => x.GetRandomFileName()).Returns(PROBE_NAME);
        PathService.Setup(x => x.Combine(DST, PROBE_NAME)).Returns(PROBE_PATH);

        FileSystem.Setup(x => x.FileExists(PROBE_PATH)).Returns(false);
        FileSystem.Setup(x => x.CreateNewFile(PROBE_PATH)).Returns(new MemoryStream());
        FileSystem.Setup(x => x.DeleteFile(PROBE_PATH));

        return this;
    }

    public InputValidatorBuilder WithNested()
    {
        PathService.Setup(x => x.NormalizePath(SRC)).Returns(SRC);
        PathService.Setup(x => x.NormalizePath(DST)).Returns(DST);
        PathService.Setup(x => x.DirectoriesAreNested(SRC, DST)).Returns(true);
        return this;
    }

    public InputValidatorBuilder WithSourceExists(bool exists)
    {
        FileSystem.Setup(x => x.DirectoryExists(SRC)).Returns(exists);
        return this;
    }

    public InputValidatorBuilder WithDestinationCreatedThenExists()
    {
        FileSystem.SetupSequence(x => x.DirectoryExists(DST))
          .Returns(false)
          .Returns(true);

        FileSystem.Setup(x => x.CreateDirectory(DST));
        return this;
    }

    public InputValidatorBuilder WithDestinationNeverExistsEvenAfterCreate()
    {
        FileSystem.Setup(x => x.DirectoryExists(DST)).Returns(false);
        FileSystem.Setup(x => x.CreateDirectory(DST));
        return this;
    }

    public InputValidatorBuilder WithReadableThrows(string path, Exception ex)
    {
        FileSystem.Setup(x => x.EnumerateFileSystemEntries(path)).Throws(ex);
        return this;
    }

    public InputValidatorBuilder WithDestinationWriteThrows(Exception ex)
    {
        // we ensure that the "write" branch reaches CreateNewFile and crashes there
        PathService.Setup(x => x.GetRandomFileName()).Returns(PROBE_NAME);
        PathService.Setup(x => x.Combine(DST, PROBE_NAME)).Returns(PROBE_PATH);

        FileSystem.Setup(x => x.FileExists(PROBE_PATH)).Returns(false);
        FileSystem.Setup(x => x.CreateNewFile(PROBE_PATH)).Throws(ex);
        return this;
    }
}