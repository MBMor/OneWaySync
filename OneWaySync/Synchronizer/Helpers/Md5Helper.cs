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
        public string ComputeMd5Hex(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
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
            if (!Md5Equals(sourceFile, destinationFile))
                throw new IOException($"MD5 mismatch after copy: {relativePath}");
        }
    }

}
