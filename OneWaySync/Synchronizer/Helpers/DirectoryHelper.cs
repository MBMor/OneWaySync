using Microsoft.Extensions.Logging;

namespace OneWaySync.Synchronizer.Helpers
{
    public interface IDirectoryHelper
    {
        DirectoryContent ScanDirectory(string rootDirectory, EnumerationOptions _enumOptions);
    }
    public sealed record FileMetadata(string FullPath, long FileSizeInBytes, DateTime LastWriteTimeUtc);

    public sealed class DirectoryContent
    {
        public required string RootDirectory { get; init; }
        public required HashSet<string> SubDirsRelativePaths { get; init; }
        public required Dictionary<string, FileMetadata> FilesRelativePathsAndMetadata { get; init; }
    }

    public class DirectoryHelper(ILogger logger) : IDirectoryHelper
    {
        private readonly ILogger _logger = logger;

        public DirectoryContent ScanDirectory(string rootDirectory, EnumerationOptions _enumOptions) {

            var subDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in Directory.EnumerateFileSystemEntries(rootDirectory, "*", _enumOptions))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(rootDirectory, item);

                    var fileAttributes = File.GetAttributes(item);
                    var isDirectory = (fileAttributes & FileAttributes.Directory) != 0;

                    if (isDirectory)
                    {
                        subDirectories.Add(relativePath);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(item);
                        files[relativePath] = new FileMetadata(item, fileInfo.Length, fileInfo.LastWriteTimeUtc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during work with item: {item} in directory {dir}| Exception: {Message}", 
                        item, rootDirectory, ex.Message);
                }
            }

            return new DirectoryContent
            {
                RootDirectory = rootDirectory,
                SubDirsRelativePaths = subDirectories,
                FilesRelativePathsAndMetadata = files
            };
        }
    }


}
