using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OneWaySync.CLIParser;
using OneWaySync.GlobalHelpers;

namespace OneWaySync.Tests;

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
        inputValidatorBuilder._fileSystem.Verify(x => x.CreateNewFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
        inputValidatorBuilder._fileSystem.Verify(x => x.DeleteFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
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

        builder._fileSystem.Verify(x => x.CreateDirectory(InputValidatorBuilder.DST), Times.Once);
        builder._fileSystem.Verify(x => x.CreateNewFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
        builder._fileSystem.Verify(x => x.DeleteFile(InputValidatorBuilder.PROBE_PATH), Times.Once);
    }

    [Test]
    public void Validate_DestinationMissing_AndCreateDoesNotHelp_ThrowsArgumentException()
    {
        var builder = new InputValidatorBuilder()
            .HappyPath()
            .WithDestinationNeverExistsEvenAfterCreate();

        var ex = Assert.Throws<ArgumentException>(() => builder.Sut.Validate(builder.ValidInput()));
        Assert.That(ex!.Message, Does.Contain("cannot be created"));

        builder._fileSystem.Verify(x => x.CreateDirectory(InputValidatorBuilder.DST), Times.Once);
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

        builder._fileSystem.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
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

        builder._fileSystem.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }
}


internal sealed class InputValidatorBuilder
{
    public Mock<IFileSystem> _fileSystem { get; }
    public Mock<IPathService> _pathService { get; }
    public Mock<ICLIParser> _cliParser { get; }
    public InputValidator Sut { get; }

    public const string SRC = "SRC";
    public const string DST = "DST";
    public const string PROBE_NAME = "probe.tmp";
    public const string PROBE_PATH = @"DST\probe.tmp";

    public InputValidatorBuilder(MockBehavior behavior = MockBehavior.Strict)
    {
        _fileSystem = new Mock<IFileSystem>(behavior);
        _pathService = new Mock<IPathService>(behavior);
        _cliParser = new Mock<ICLIParser>(behavior);

        Sut = new InputValidator(NullLogger.Instance, _fileSystem.Object, _pathService.Object, _cliParser.Object);
    }

    public UserInput ValidInput(string? src = SRC, string? dst = DST)
        => new() { SourceDirectory = src!, DestinationDirectory = dst! };

    public InputValidatorBuilder HappyPath()
    {
        // path normalization + nesting
        _pathService.Setup(x => x.NormalizePath(SRC)).Returns(SRC);
        _pathService.Setup(x => x.NormalizePath(DST)).Returns(DST);
        _pathService.Setup(x => x.DirectoriesAreNested(SRC, DST)).Returns(false);

        // existence
        _fileSystem.Setup(x => x.DirectoryExists(SRC)).Returns(true);
        _fileSystem.Setup(x => x.DirectoryExists(DST)).Returns(true);

        // readable checks
        _fileSystem.Setup(x => x.EnumerateFileSystemEntries(SRC)).Returns(Array.Empty<string>());
        _fileSystem.Setup(x => x.EnumerateFileSystemEntries(DST)).Returns(Array.Empty<string>());

        // probe file write/delete
        _pathService.Setup(x => x.GetRandomFileName()).Returns(PROBE_NAME);
        _pathService.Setup(x => x.Combine(DST, PROBE_NAME)).Returns(PROBE_PATH);

        _fileSystem.Setup(x => x.FileExists(PROBE_PATH)).Returns(false);
        _fileSystem.Setup(x => x.CreateNewFile(PROBE_PATH)).Returns(new MemoryStream());
        _fileSystem.Setup(x => x.DeleteFile(PROBE_PATH));

        return this;
    }

    public InputValidatorBuilder WithNested()
    {
        _pathService.Setup(x => x.NormalizePath(SRC)).Returns(SRC);
        _pathService.Setup(x => x.NormalizePath(DST)).Returns(DST);
        _pathService.Setup(x => x.DirectoriesAreNested(SRC, DST)).Returns(true);
        return this;
    }

    public InputValidatorBuilder WithSourceExists(bool exists)
    {
        _fileSystem.Setup(x => x.DirectoryExists(SRC)).Returns(exists);
        return this;
    }

    public InputValidatorBuilder WithDestinationCreatedThenExists()
    {
        _fileSystem.SetupSequence(x => x.DirectoryExists(DST))
          .Returns(false)
          .Returns(true);

        _fileSystem.Setup(x => x.CreateDirectory(DST));
        return this;
    }

    public InputValidatorBuilder WithDestinationNeverExistsEvenAfterCreate()
    {
        _fileSystem.Setup(x => x.DirectoryExists(DST)).Returns(false);
        _fileSystem.Setup(x => x.CreateDirectory(DST));
        return this;
    }

    public InputValidatorBuilder WithReadableThrows(string path, Exception ex)
    {
        _fileSystem.Setup(x => x.EnumerateFileSystemEntries(path)).Throws(ex);
        return this;
    }

    public InputValidatorBuilder WithDestinationWriteThrows(Exception ex)
    {
        // we ensure that the "write" branch reaches CreateNewFile and crashes there
        _pathService.Setup(x => x.GetRandomFileName()).Returns(PROBE_NAME);
        _pathService.Setup(x => x.Combine(DST, PROBE_NAME)).Returns(PROBE_PATH);

        _fileSystem.Setup(x => x.FileExists(PROBE_PATH)).Returns(false);
        _fileSystem.Setup(x => x.CreateNewFile(PROBE_PATH)).Throws(ex);
        return this;
    }
}