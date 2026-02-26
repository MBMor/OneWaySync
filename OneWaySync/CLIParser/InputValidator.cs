using CommandLine;
using Microsoft.Extensions.Logging;

namespace OneWaySync.CLIParser
{
    public class InputValidator(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        public static UserInput GetCLIData(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<Options>(args);

            return new UserInput
            {
                SourceDirectory = NormalizePath(parsed.Value.SourceDirectoryPath),
                DestinationDirectory = NormalizePath(parsed.Value.DestinationDirectoryPath),
                SynchronizationInterval = parsed.Value.SynchronizationInterval == 0
                                            ? 1 : Math.Abs(parsed.Value.SynchronizationInterval), // min value 1
                LogFilePath = NormalizePath(parsed.Value.LogFilePath)
            };
        }
                
        public void Validate(UserInput userInput)
        {
            var source = userInput.SourceDirectory;
            var destination = userInput.DestinationDirectory;

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Whitespace/null used instead of valid directory path"); 

            if (DirectoriesAreNested(source, destination))
                throw new ArgumentException("Nested Source and Destination directory");

            //source must exist and allow reading
            if (DirectoryMissing(source) || DirectoryReadingNotPossible(source))
                throw new ArgumentException
                    ($"Source directory doesn't exist or inaccessible- {source}");

            //destination directory can be created if missing
            if (DirectoryMissing(destination))
            {
                _logger.LogWarning("Destination directory doesn't exist, trying to create one");
                Directory.CreateDirectory(destination);
            }

            //destination must exist and allow reading
            if (DirectoryMissing(destination) || DirectoryReadingNotPossible(destination))
                throw new ArgumentException
                    ($"Destination directory doesn't exist or inaccessible- {destination}");

            //destination write is permited
            if (DirectoryWriteIsNotPermitted(destination))
                throw new UnauthorizedAccessException
                    ($"No permission for writing in destination directory {destination}");
        }

        private bool DirectoryMissing(string path)
        {
            if (Directory.Exists(path))
            {
                _logger.LogInformation("Directory {path} exist", path);
                return false;
            }
            else
            {
                _logger.LogError("Directory {path} doesn't exist", path);
                return true;
            }
        }

        private bool DirectoryReadingNotPossible(string path)
        {
            try
            {
                var resultOfAccessAttempt = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
                _logger.LogInformation("Directory {path} accessible for reading", path);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Permission for reading missing - directory: {path}", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Inaccessible directory: {path}. Exception: {Message}", path, ex.Message);
                return true;
            }
        }

        private bool DirectoryWriteIsNotPermitted(string path)
        {
            try
            {
                const int maxAttemptsForRandomNameGenerator = 10;
                string testWriteDeletePermissionFile = "";

                for (int i = 0; i < maxAttemptsForRandomNameGenerator; i++)
                {
                    string generatedRandomFileName = Path.Combine(path, Path.GetRandomFileName());
                    if (!File.Exists(generatedRandomFileName))
                    {
                        testWriteDeletePermissionFile = generatedRandomFileName;
                        break;
                    }
                }

                using (FileStream fs = new(
                    testWriteDeletePermissionFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    //dummy file creation to verify access rights
                }

                File.Delete(testWriteDeletePermissionFile);
                _logger.LogInformation("Directory {path} Write/Delete permission OK", path);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("You aren't authorized to write in directory: {path}", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Can't write in directory: {path}. Exception: {Message}", path, ex.Message);
                return true;
            }
        }
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        private static bool DirectoriesAreNested(string path1, string path2)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            // Same directories are not allowed
            if (string.Equals(path1, path2, comparison))
                return true;

            // Can't be subdirectory
            if (path1.StartsWith(path2 + Path.DirectorySeparatorChar, comparison))
                return true;

            if (path2.StartsWith(path1 + Path.DirectorySeparatorChar, comparison))
                return true;

            return false;
        }

    }
}
