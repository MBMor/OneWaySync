using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace OneWaySync.Synchronizer
{
    internal class Md5Helper
    {
        private readonly ILogger _logger;
        public Md5Helper(ILogger logger)
        {
            _logger = logger;
        }
        public static string ComputeMd5Hex(string filePath)
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

        public bool Md5Equals(string sourceFile, string destinationFile, string relativePath)
        {
            var src = ComputeMd5Hex(sourceFile);
            var dst = ComputeMd5Hex(destinationFile);
            return StringComparer.OrdinalIgnoreCase.Equals(src, dst);
        }

        public void ValidateCopy(string sourceFile, string destinationFile, string relativePath)
        {
            if (!Md5Equals(sourceFile, destinationFile, relativePath))
                throw new IOException($"MD5 mismatch after copy: {relativePath}");
        }
    }
}
