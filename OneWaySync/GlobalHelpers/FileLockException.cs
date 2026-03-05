using System;
using System.IO;

namespace OneWaySync.GlobalHelpers
{
    public abstract class FileLockException : IOException
    {
        protected FileLockException(string message, string path, IOException inner)
            : base(message, inner)
        {
            Path = path;
        }

        public string Path { get; }
    }

    public sealed class SourceLockedException : FileLockException
    {
        public SourceLockedException(string sourcePath, IOException inner)
            : base($"Source file is locked/in use: {sourcePath}", sourcePath, inner)
        {
        }
    }

    public sealed class DestinationLockedException : FileLockException
    {
        public DestinationLockedException(string destinationPath, IOException inner)
            : base($"Destination file is locked/in use: {destinationPath}", destinationPath, inner)
        {
        }
    }
}