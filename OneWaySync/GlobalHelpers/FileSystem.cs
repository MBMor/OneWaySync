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
            => File.Copy(src, dst, overwrite);

        public void DeleteFile(string path)
            => File.Delete(path);

        public Stream CreateNewFile(string path)
            => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        public void SetLastWriteTimeUtc(string path, DateTime utc)
            => File.SetLastWriteTimeUtc(path, utc);

        public FileAttributes GetAttributes(string path)
            => File.GetAttributes(path);

        public void SetAttributes(string path, FileAttributes attributes)
            => File.SetAttributes(path, attributes);

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
    }
}
