using Microsoft.Extensions.Logging;
using OneWaySync.CLIParser;

namespace OneWaySync.Synchronizer
{
    internal class Synchronizer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _source;
        private readonly string _destination;
        private readonly TimeSpan _synchronizationPeriod;
        private readonly DirectoryHelper _directoryMetadataHelper;

        private Timer? _timer = null;
        private bool _isSyncRunning = false;
        private static readonly EnumerationOptions _enumOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        public Synchronizer(ILogger logger, UserInput userInput)
        {
            _logger = logger;
            _source = userInput.SourceDirectory!;
            _destination = userInput.DestinationDirectory!;
            _synchronizationPeriod = TimeSpan.FromSeconds(userInput.SynchronizationInterval);
            _directoryMetadataHelper = new DirectoryHelper(logger); 

        }

        public void Start()
        {
            if (_timer != null)
                return;

            // Runs immediately RunOnce() method after start, then periodically
            _timer = new Timer(_ => RunOnce(), null, TimeSpan.Zero, _synchronizationPeriod);

            _logger.LogInformation("Synchronization started. Period: {Period}", _synchronizationPeriod);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _logger.LogInformation("Synchronization stopped.");
        }

        public void RunOnce()
        {
            if (_isSyncRunning)
            {
                _logger.LogWarning("Skipping start of synchronization cycle, previous one still in progress");
                return;
            }
            DisplayVisualSeparatorInConsole();

            _isSyncRunning = true;
            try
            {
                var sourceStructure = _directoryMetadataHelper.ScanDirectory(_source, _enumOptions);
                var destinationStructure = _directoryMetadataHelper.ScanDirectory(_destination, _enumOptions);

                CreateSubDirectories(sourceStructure, destinationStructure);

                CopyOrUpdateFiles(sourceStructure, destinationStructure);

                DeleteExtraFiles(sourceStructure, destinationStructure);

                DeleteExtraDirectories(sourceStructure, destinationStructure);

                _logger.LogInformation("Synchronization finished. Waiting for new round to start.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Sync failed: {Message}", ex.Message);
            }
            finally
            {
                _isSyncRunning = false;
            }
        }

        private static void DisplayVisualSeparatorInConsole()
        {
            Console.WriteLine(" ");
            Console.WriteLine("||=======================================||");
            Console.WriteLine("||Press \"Enter\" key to end the program.  ||");
            Console.WriteLine("||=======================================||");
            Console.WriteLine("Starting new synchronization round");
            Console.WriteLine(" ");
        }

        private void CreateSubDirectories(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var relativePath in sourceStructure.SubDirsRelativePaths.OrderBy(subDir => subDir.Length))
            {
                try
                {
                    var dstDirectoryFullPath = Path.Combine(destinationStructure.RootDirectory, relativePath);

                    if (!Directory.Exists(dstDirectoryFullPath))
                    {
                        Directory.CreateDirectory(dstDirectoryFullPath);
                        _logger.LogInformation("Created directory: {Dir}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed creating directory: {Dir} | Exception: {ex.Message}", relativePath, ex.Message);
                }
            }
        }
        private void CopyOrUpdateFiles(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var (relativePath, srcFileMetadata) in sourceStructure.FilesRelativePathsAndMetadata)
            {
                try
                {
                    var dstFileFullPath = Path.Combine(destinationStructure.RootDirectory, relativePath);
                    //if file is missing copy it to destination and skip to another foreach item, if not query dstFileMetadata
                    if (!destinationStructure.FilesRelativePathsAndMetadata
                                                    .TryGetValue(relativePath, out var dstFileMetadata))
                    {
                        CopyFileSetMetadataAndCheckMd5(
                            srcFileMetadata.FullPath, 
                            relativePath, 
                            dstFileFullPath,
                            srcFileMetadata.LastWriteTimeUtc); 

                        continue;
                    }

                    bool metadataAreDifferent =
                        srcFileMetadata.FileSizeInBytes != dstFileMetadata.FileSizeInBytes ||
                        srcFileMetadata.LastWriteTimeUtc != dstFileMetadata.LastWriteTimeUtc;

                    bool contentMismatchByMd5 = false;
                    if (!metadataAreDifferent)
                    {
                        contentMismatchByMd5 = !Md5Helper.Md5Equals(srcFileMetadata.FullPath, dstFileMetadata.FullPath);
                        if (contentMismatchByMd5)
                            _logger.LogWarning("Metadata equal but content differs (MD5 mismatch): {File}", relativePath);
                    }

                    if (metadataAreDifferent || contentMismatchByMd5)
                    {
                        CopyFileSetMetadataAndCheckMd5(
                            srcFileMetadata.FullPath,
                            relativePath,
                            dstFileFullPath,
                            srcFileMetadata.LastWriteTimeUtc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError
                        ("Failed processing file: {File} | Exception: {ex.Message}", srcFileMetadata.FullPath, ex.Message);
                }
            }
        }


        private void DeleteExtraFiles(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var (relativePath, dstFileMetadata) in destinationStructure.FilesRelativePathsAndMetadata)
            {
                if (sourceStructure.FilesRelativePathsAndMetadata.ContainsKey(relativePath))
                    continue;

                try
                {
                    File.SetAttributes(dstFileMetadata.FullPath, FileAttributes.Normal);
                    File.Delete(dstFileMetadata.FullPath);
                    _logger.LogInformation("Deleted extra file: {File}", relativePath);
                    
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete extra file: {File} | Exception: {ex.Message}", relativePath, ex.Message);
                }
            }
        }

        private void DeleteExtraDirectories(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var relativePath in destinationStructure.SubDirsRelativePaths
                                                                    .OrderByDescending(subDir => subDir.Length))
            {
                if (sourceStructure.SubDirsRelativePaths.Contains(relativePath))
                    continue;

                var destinationDirectryFullPath = Path.Combine(destinationStructure.RootDirectory, relativePath);

                try
                {
                    if (Directory.Exists(destinationDirectryFullPath))
                    {
                        Directory.Delete(destinationDirectryFullPath, recursive: true);
                        _logger.LogInformation("Deleted extra directory: {Dir}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete extra directory: {Dir} | Exception: {Message}",
                        relativePath, ex.Message);
                }
            }
        }

        private void CopyFileSetMetadataAndCheckMd5(
            string sourceFile, 
            string relativePath, 
            string replicaFilePath,
            DateTime lastWriteTimeUtc)
        {
            File.Copy(sourceFile, replicaFilePath, overwrite: true);
            File.SetLastWriteTimeUtc(replicaFilePath, lastWriteTimeUtc);

            Md5Helper.ValidateCopy(sourceFile, replicaFilePath, relativePath);
            _logger.LogInformation("Copied file (MD5 OK): {File}", relativePath);
        }
        public void Dispose()
        {
            Stop();
        }
    }
}
