using OneWaySync.GlobalHelpers;
using System.Security.Cryptography;

namespace OneWaySync.Synchronizer.Helpers
{
    public interface IMd5Helper
    {
        string ComputeMd5Hex(string filePath);
        bool Md5Equals(string sourceFile, string destinationFile);
        void ValidateCopyOrThrowException(string sourceFile, string destinationFile, string relativePath);
    }
    public class Md5Helper : IMd5Helper
    {
        private readonly IFileSystem _fileSystem;

        public Md5Helper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }
        public string ComputeMd5Hex(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan); 

            var hash = md5.ComputeHash(stream);
            return Convert.ToHexString(hash); 
        }

        public bool Md5Equals(string sourceFile, string destinationFile)
        {
            var src = ComputeMd5Hex(sourceFile);
            var dst = ComputeMd5Hex(destinationFile);
            return StringComparer.OrdinalIgnoreCase.Equals(src, dst);
        }

        public void ValidateCopyOrThrowException(string sourceFile, string destinationFile, string relativePath)
        {
            try
            {
                if (!Md5Equals(sourceFile, destinationFile))
                    throw new IOException($"MD5 mismatch after copy: {relativePath}");
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
            {
                // destination locked?
                if (_fileSystem.FileExists(destinationFile) &&
                    _fileSystem.IsFileLocked(destinationFile))
                {
                    throw new DestinationLockedException(destinationFile, ex);
                }

                // else source is considered locked
                throw new SourceLockedException(sourceFile, ex);
            }
        }
    }

}
