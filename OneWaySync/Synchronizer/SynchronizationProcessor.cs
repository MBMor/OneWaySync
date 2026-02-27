using Microsoft.Extensions.Logging;
using OneWaySync.GlobalHelpers;
using OneWaySync.Synchronizer.Helpers;

namespace OneWaySync.Synchronizer
{
    internal interface ISynchronizationProcessor
    {
        void RunOnce();
    }
    internal class SynchronizationProcessor : ISynchronizationProcessor
    {
        private readonly ILogger _logger;
        private readonly IDirectoryScaner _directoryMetadataHelper;
        private readonly IMd5Helper _md5Helper;
        private readonly IFileOperationsHelper _fileOperationsHelper;

        private readonly string _source;
        private readonly string _destination;

        private readonly EnumerationOptions _enumOptions;

        public SynchronizationProcessor(
            ILogger logger, 
            string sourceDirectory,
            string destinationDirectory,
            IDirectoryScaner directoryHelper, 
            IMd5Helper md5Helper,
            IFileOperationsHelper fileOperationsHelper,
            EnumerationOptions? enumOptionsOptionalOrDefault = null
            )
        {
            _logger = logger;
            _source = sourceDirectory;
            _destination = destinationDirectory;
            _directoryMetadataHelper = directoryHelper;
            _md5Helper = md5Helper;
            _fileOperationsHelper = fileOperationsHelper;
            _enumOptions = enumOptionsOptionalOrDefault ?? new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };
    }

        public void RunOnce()
        {
            DisplayVisualSeparatorInConsole();

            var sourceStructure = _directoryMetadataHelper.ScanDirectory(_source, _enumOptions);
            var destinationStructure = _directoryMetadataHelper.ScanDirectory(_destination, _enumOptions);

            CreateSubDirectories(sourceStructure, destinationStructure);

            CopyOrUpdateFiles(sourceStructure, destinationStructure);

            DeleteExtraFiles(sourceStructure, destinationStructure);

            DeleteExtraDirectories(sourceStructure, destinationStructure);

        }

        private void CreateSubDirectories(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var relativePath in sourceStructure.SubDirsRelativePaths.OrderBy(subDir => subDir.Length))
            {
                try
                {
                    var dstDirectoryFullPath = _fileOperationsHelper.Combine(destinationStructure.RootDirectory, relativePath);

                    if (!_fileOperationsHelper.DirectoryExists(dstDirectoryFullPath))
                    {
                        _fileOperationsHelper.CreateDirectory(dstDirectoryFullPath);
                        _logger.LogInformation("Created directory: {Dir}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed creating directory: {Dir} | Exception: {Message}", relativePath, ex.Message);
                }
            }
        }

        private void CopyOrUpdateFiles(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var (relativePath, srcFileMetadata) in sourceStructure.FilesRelativePathsAndMetadata)
            {
                try
                {
                    ProcessCurrentFile(relativePath, srcFileMetadata, destinationStructure);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing file: {File}", srcFileMetadata.FullPath);
                }
            }
        }
        private void ProcessCurrentFile(
            string relativePath, 
            FileMetadata srcFileMetadata, 
            DirectoryContent destinationStructure)
        {
            var dstFileFullPath =
                _fileOperationsHelper.Combine(destinationStructure.RootDirectory, relativePath);
            //if file is missing copy it to destination and skip to another foreach item, if not query dstFileMetadata
            if (!destinationStructure.FilesRelativePathsAndMetadata
                    .TryGetValue(relativePath, out var dstFileMetadata))
            {
                CopyFileSetMetadataAndCheckMd5(
                    srcFileMetadata.FullPath,
                    relativePath,
                    dstFileFullPath,
                    srcFileMetadata.LastWriteTimeUtc);

                return;
            }

            if (ShouldCopyFile(relativePath, srcFileMetadata, dstFileMetadata))
            {
                CopyFileSetMetadataAndCheckMd5(
                    srcFileMetadata.FullPath,
                    relativePath,
                    dstFileFullPath,
                    srcFileMetadata.LastWriteTimeUtc);
            }
        }
        private bool ShouldCopyFile(
            string relativePath,
            FileMetadata srcFileMetadata,
            FileMetadata dstFileMetadata)
        {
            bool metadataAreDifferent =
                srcFileMetadata.FileSizeInBytes != dstFileMetadata.FileSizeInBytes ||
                srcFileMetadata.LastWriteTimeUtc != dstFileMetadata.LastWriteTimeUtc;

            if (metadataAreDifferent)
                return true;

            bool contentMismatchByMd5 = !_md5Helper.Md5Equals(srcFileMetadata.FullPath, dstFileMetadata.FullPath);

            if (contentMismatchByMd5)
                _logger.LogWarning(
                    "Metadata equal but content differs (MD5 mismatch): {File}",
                    relativePath);

            return contentMismatchByMd5;
        }

        private void DeleteExtraFiles(DirectoryContent sourceStructure, DirectoryContent destinationStructure)
        {
            foreach (var (relativePath, dstFileMetadata) in destinationStructure.FilesRelativePathsAndMetadata)
            {
                if (sourceStructure.FilesRelativePathsAndMetadata.ContainsKey(relativePath))
                    continue;

                try
                {
                    _fileOperationsHelper.SetAttributes(dstFileMetadata.FullPath, FileAttributes.Normal);
                    _fileOperationsHelper.DeleteFile(dstFileMetadata.FullPath);
                    _logger.LogInformation("Deleted extra file: {File}", relativePath);
                    
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete extra file: {File} | Exception: {Message}", relativePath, ex.Message);
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

                var destinationDirectryFullPath = 
                    _fileOperationsHelper.Combine(destinationStructure.RootDirectory, relativePath);

                try
                {
                    if (_fileOperationsHelper.DirectoryExists(destinationDirectryFullPath))
                    {
                        _fileOperationsHelper.DeleteDirectory(destinationDirectryFullPath, recursive: true);
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
            CopyFile(sourceFile, replicaFilePath);
            SetLastWriteTimeUtc(replicaFilePath, lastWriteTimeUtc);

            ValidateCopyOrThrowException(sourceFile, replicaFilePath, relativePath);

            _logger.LogInformation("Copied file (MD5 OK): {File}", relativePath);
        }

        private void CopyFile(string sourceFile, string replicaFilePath)
        {
            _fileOperationsHelper.CopyFile(sourceFile, replicaFilePath, overwrite: true);
        }

        private void SetLastWriteTimeUtc(string replicaFilePath, DateTime lastWriteTimeUtc)
        {
            _fileOperationsHelper.SetLastWriteTimeUtc(replicaFilePath, lastWriteTimeUtc);
        }

        private void ValidateCopyOrThrowException(string sourceFile, string replicaFilePath, string relativePath)
        {
            _md5Helper.ValidateCopyOrThrowException(sourceFile, replicaFilePath, relativePath);
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
    }
}
