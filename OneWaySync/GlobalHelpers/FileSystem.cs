using System;
using System.Collections.Generic;
using System.IO;

namespace OneWaySync.GlobalHelpers
{
    public interface IFileSystem
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive);

        bool FileExists(string path);
        void CopyFile(string src, string dst, bool overwrite);
        void DeleteFile(string path);
        Stream CreateNewFile(string path);
        bool IsFileLocked(string path);
        void SetLastWriteTimeUtc(string path, DateTime utc);
        FileAttributes GetAttributes(string path);
        void SetAttributes(string path, FileAttributes attributes);

        IEnumerable<string> EnumerateFileSystemEntries(string path);
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions options);

        void FilePathAllowsCreateOrUseFile(string path);
        (long Length, DateTime LastWriteTimeUtc) GetFileSizeAndLastWriteTimeUtc(string path);
    }
    internal class FileSystem : IFileSystem
    {
        public bool DirectoryExists(string path)
            => Directory.Exists(path);

        public void CreateDirectory(string path)
            => Directory.CreateDirectory(path);

        public void DeleteDirectory(string path, bool recursive)
            => Directory.Delete(path, recursive);

        public bool FileExists(string path)
            => File.Exists(path);

        public void CopyFile(string src, string dst, bool overwrite)
        {
            try
            {
                File.Copy(src, dst, overwrite);
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                // Destination lock? (when overwriting existting dst, or is hold by someone)
                if (File.Exists(dst) && IsFileLocked(dst))
                    throw new DestinationLockedException(dst, ex);

                // else we take the source as locked
                throw new SourceLockedException(src, ex);
            }
        }

        public void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                throw new DestinationLockedException(path, ex);
            }
        }

        public Stream CreateNewFile(string path)
        {
            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                throw new DestinationLockedException(path, ex);
            }
        }
        public bool IsFileLocked(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        public void SetLastWriteTimeUtc(string path, DateTime utc)
        {
            try
            {
                File.SetLastWriteTimeUtc(path, utc);
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                throw new DestinationLockedException(path, ex);
            }
        }

        public FileAttributes GetAttributes(string path)
            => File.GetAttributes(path);

        public void SetAttributes(string path, FileAttributes attributes)
        {
            try
            {
                File.SetAttributes(path, attributes);
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                throw new DestinationLockedException(path, ex);
            }
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path)
            => Directory.EnumerateFileSystemEntries(path);

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions options)
            => Directory.EnumerateFileSystemEntries(path, searchPattern, options);

        public void FilePathAllowsCreateOrUseFile(string path)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Invalid log file path.");

            CreateDirectory(directory);

            using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
        }

        public (long Length, DateTime LastWriteTimeUtc) GetFileSizeAndLastWriteTimeUtc(string path)
        {
            var fileMetadata = new FileInfo(path);
            return (fileMetadata.Length, fileMetadata.LastWriteTimeUtc);
        }
        private static bool IsSharingOrLockViolation(IOException ex)
        {
            int win32 = ex.HResult & 0xFFFF;
            return win32 is 32 or 33; // sharing/lock violation
        }
    }
}
