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
        private readonly Md5Helper _md5Helper;

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
            _md5Helper = new Md5Helper(logger);

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
            Console.WriteLine(" ");
            Console.WriteLine("||=======================================||");
            Console.WriteLine("||Press \"Enter\" key to end the program.  ||");
            Console.WriteLine("||=======================================||");
            Console.WriteLine("Starting new synchronization round");
            Console.WriteLine(" ");

            _isSyncRunning = true;
            try
            {
                CreateSubDirectoriesInDestination();

                CopyOrUpdateFilesInDestination();

                DeleteExtraFilesInReplica();

                DeleteExtraDirectoriesInReplica();

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

        private void CreateSubDirectoriesInDestination()
        {
            var allSubDirectoriesInSource = Directory.EnumerateDirectories(_source, "*", _enumOptions);
            foreach (var subDirectory in allSubDirectoriesInSource)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_source, subDirectory);
                    var replicaSubDirectory = Path.Combine(_destination, relativePath);

                    if (!Directory.Exists(replicaSubDirectory))
                    {
                        Directory.CreateDirectory(replicaSubDirectory);
                        _logger.LogInformation("Created directory: {Dir}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed creating directory: {Dir} | Exception: {ex.Message}", subDirectory, ex.Message);
                }
            }
        }
        private void CopyOrUpdateFilesInDestination()
        {
            var allFilesInSourceDirectory = Directory.EnumerateFiles(_source, "*", _enumOptions);
            foreach (var sourceFile in allFilesInSourceDirectory)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_source, sourceFile);
                    var replicaFilePath = Path.Combine(_destination, relativePath);

                    var sourceFileInfo = new FileInfo(sourceFile);

                    if (!File.Exists(replicaFilePath))
                    {
                        CopyFileSetMetadataAndCheckMd5(sourceFile, relativePath, replicaFilePath, sourceFileInfo);

                        continue;
                    }

                    var replicaInfo = new FileInfo(replicaFilePath);

                    bool metadataAreDifferent =
                        sourceFileInfo.Length != replicaInfo.Length ||
                        sourceFileInfo.LastWriteTimeUtc != replicaInfo.LastWriteTimeUtc;

                    bool contentMismatchByMd5 = false;
                    if (!metadataAreDifferent)
                    {
                        contentMismatchByMd5 = !_md5Helper.Md5Equals(sourceFile, replicaFilePath, relativePath);
                        if (contentMismatchByMd5)
                            _logger.LogWarning("Metadata equal but content differs (MD5 mismatch): {File}", relativePath);
                    }

                    if (metadataAreDifferent || contentMismatchByMd5)
                    {
                        CopyFileSetMetadataAndCheckMd5(sourceFile, relativePath, replicaFilePath, sourceFileInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed processing file: {File} | Exception: {ex.Message}", sourceFile, ex.Message);
                }
            }
        }


        private void DeleteExtraFilesInReplica()
        {
            var allFilesInDestinationDirectory = Directory.EnumerateFiles(_destination, "*", _enumOptions);
            foreach (var replicaFile in allFilesInDestinationDirectory)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_destination, replicaFile);
                    var sourceFilePath = Path.Combine(_source, relativePath);

                    if (!File.Exists(sourceFilePath))
                    {
                        File.SetAttributes(replicaFile, FileAttributes.Normal);
                        File.Delete(replicaFile);
                        _logger.LogInformation("Deleted extra file: {File}", replicaFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete extra file: {File} | Exception: {ex.Message}", replicaFile, ex.Message);
                }
            }
        }

        private void DeleteExtraDirectoriesInReplica()
        {
            var allSubdirectoriesInDestinationDirectory = Directory
                .EnumerateDirectories(_destination, "*", _enumOptions)
                .OrderByDescending(d => d.Length); 

            foreach (var replicaSubdirectory in allSubdirectoriesInDestinationDirectory)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_destination, replicaSubdirectory);
                    var sourceDirPath = Path.Combine(_source, relativePath);

                    if (!Directory.Exists(sourceDirPath))
                    {
                        Directory.Delete(replicaSubdirectory, recursive: true);
                        _logger.LogInformation("Deleted extra directory: {Dir}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete extra directory: {Dir} | Exception: {Message}",
                        replicaSubdirectory, ex.Message);
                }
            }
        }

        private void CopyFileSetMetadataAndCheckMd5(string sourceFile, string relativePath, string replicaFilePath, FileInfo sourceFileInfo)
        {
            File.Copy(sourceFile, replicaFilePath, overwrite: true);
            File.SetLastWriteTimeUtc(replicaFilePath, sourceFileInfo.LastWriteTimeUtc);

            _md5Helper.ValidateCopy(sourceFile, replicaFilePath, relativePath);
            _logger.LogInformation("Copied file (MD5 OK): {File}", relativePath);
        }
        public void Dispose()
        {
            Stop();
        }
    }
}
