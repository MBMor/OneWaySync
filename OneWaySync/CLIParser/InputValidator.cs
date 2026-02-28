using CommandLine;
using Microsoft.Extensions.Logging;
using OneWaySync.GlobalHelpers;

namespace OneWaySync.CLIParser
{
    public class InputValidator(
        ILogger logger,
        IFileSystem fileOperationsHelper,
        IPathService pathService,
        ICLIParser cliParser)
    {
        private readonly ILogger _logger = logger;
        private readonly IFileSystem _fileOperationsHelper = fileOperationsHelper;
        private readonly IPathService _pathService = pathService;
        private readonly ICLIParser _cliParser = cliParser;

        public UserInput GetCLIData(string[] args)
        {
            return _cliParser.Parse(args);
        }

        public void Validate(UserInput userInput)
        {
            var source = NormalizeRequiredPath(
                userInput.SourceDirectory,
                nameof(userInput.SourceDirectory));

            var destination = NormalizeRequiredPath(
                userInput.DestinationDirectory,
                nameof(userInput.DestinationDirectory));

            DirectoriesNotNestedCheck(source, destination);

            DirectoryExistsAndIsReadable(
                path: source,
                roleName: "Source",
                canBeCreatedIfMissing: false);

            DirectoryExistsAndIsReadable(
                path: destination,
                roleName: "Destination",
                canBeCreatedIfMissing: true);

            DirectoryAccesibleForWritingCheck(destination);
        }

        private string NormalizeRequiredPath(string? path, string paramName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogError(
                    "Whitespace/null used instead of valid directory path - {dirType} - {value}",
                    paramName, path);

                throw new ArgumentException(
                    "Whitespace/null used instead of valid directory path",
                    paramName);
            }

            return _pathService.NormalizePath(path);
        }

        private void DirectoriesNotNestedCheck(string source, string destination)
        {
            if (_pathService.DirectoriesAreNested(source, destination))
            {
                _logger.LogError("Nested Source and Destination directory");
                throw new ArgumentException("Nested Source and Destination directory");
            }
        }

        private void DirectoryExistsAndIsReadable(string path, string roleName, bool canBeCreatedIfMissing)
        {
            DirectoryExistsCheck(path, roleName, canBeCreatedIfMissing);
            DirectoryIsReadableCheck(path, roleName);
        }

        private void DirectoryExistsCheck(string path, string roleName, bool canBeCreatedIfMissing)
        {
            // this is for destination directory check, one try to create it.
            if (!_fileOperationsHelper.DirectoryExists(path))
            {
                if (!canBeCreatedIfMissing)
                {
                    _logger.LogError("{role} directory {path} doesn't exist", roleName, path);
                    throw new ArgumentException($"{roleName} directory doesn't exist - {path}");
                }

                _logger.LogWarning("{role} directory {path} doesn't exist, trying to create one", roleName, path);
                _fileOperationsHelper.CreateDirectory(path);

                // verity that directory was created
                if (!_fileOperationsHelper.DirectoryExists(path))
                {
                    _logger.LogError("Failed to create {role} directory {path}", roleName, path);
                    throw new ArgumentException($"{roleName} directory doesn't exist or cannot be created - {path}");
                }
            }
        }

        private void DirectoryIsReadableCheck(string path, string roleName)
        {
            try
            {
                _fileOperationsHelper.EnumerateFileSystemEntries(path).FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Permission for reading missing - {role} directory: {path}", roleName, path);
                throw new UnauthorizedAccessException($"{roleName} directory is not readable - {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Inaccessible {role} directory: {path}. Exception: {Message}",
                    roleName, path, ex.Message);

                throw new ArgumentException($"{roleName} directory is inaccessible - {path}", ex);
            }
        }

        private void DirectoryAccesibleForWritingCheck(string path)
        {
            try
            {
                var probeFile = GenerateFileWithRandomName(path);

                using (var _ = _fileOperationsHelper.CreateNewFile(probeFile))
                {
                    // dummy create
                }

                _fileOperationsHelper.DeleteFile(probeFile);
                _logger.LogInformation("Directory {path} Write/Delete permission OK", path);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("You aren't authorized to write in directory: {path}", path);
                throw new UnauthorizedAccessException($"No permission for writing in destination directory {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Can't write in directory: {path}. Exception: {Message}", path, ex.Message);
                throw new IOException($"Can't write in destination directory {path}", ex);
            }
        }

        private string GenerateFileWithRandomName(string directory)
        {
            const int maxAttempts = 10;

            for (int i = 0; i < maxAttempts; i++)
            {
                var generatedRandomFileName = _pathService.Combine(directory, _pathService.GetRandomFileName());
                if (!_fileOperationsHelper.FileExists(generatedRandomFileName))
                    return generatedRandomFileName;
            }

            throw new IOException($"Unable to generate probe file name in {directory}");
        }
    }
}