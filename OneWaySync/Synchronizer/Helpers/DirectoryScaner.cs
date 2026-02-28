using Microsoft.Extensions.Logging;
using OneWaySync.GlobalHelpers;

namespace OneWaySync.Synchronizer.Helpers
{
    public interface IDirectoryScaner
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

    public class DirectoryScaner(
        ILogger logger, 
        IFileSystem fileOperationsHelper,
        IPathService pathService
        ) : IDirectoryScaner
    {
        private readonly ILogger _logger = logger;
        private readonly IFileSystem _fileOperationsHelper = fileOperationsHelper;
        private readonly IPathService _pathService = pathService; 

        public DirectoryContent ScanDirectory(string rootDirectory, EnumerationOptions enumOptions) {

            var subDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in _fileOperationsHelper.EnumerateFileSystemEntries(rootDirectory, "*", enumOptions))
            {
                try
                {
                    var relativePath = _pathService.GetRelativePath(rootDirectory, item);

                    var fileAttributes = _fileOperationsHelper.GetAttributes(item);
                    var isDirectory = (fileAttributes & FileAttributes.Directory) != 0;

                    if (isDirectory)
                    {
                        subDirectories.Add(relativePath);
                    }
                    else
                    {
                        var(length, lastWriteUtc) = _fileOperationsHelper.GetFileSizeAndLastWriteTimeUtc(item);
                        files[relativePath] = new FileMetadata(item, length, lastWriteUtc);
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
