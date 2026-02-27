//////will be deleted after switch from FileOperationsHelper to (FileSystem and PathService)

namespace OneWaySync.GlobalHelpers
{
    public interface IFileOperationsHelper
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive);

        bool FileExists(string path);
        void CopyFile(string src, string dst, bool overwrite);
        void DeleteFile(string path);
        string GetRandomFileName();
        Stream CreateNewFile(string path);
        void SetLastWriteTimeUtc(string path, DateTime utc);
        FileAttributes GetAttributes(string path);
        void SetAttributes(string path, FileAttributes attributes);

        string Combine(string path1, string path2);
        IEnumerable<string> EnumerateFileSystemEntries(string path);
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions options);

        string GetRelativePath(string relativeTo, string path);
        string NormalizePath(string path);
        bool DirectoriesAreNested(string path1, string path2);

        void FilePathAllowsCreateOrUseFile(string path);
        (long Length, DateTime LastWriteTimeUtc) GetFileSizeAndLastWriteTimeUtc(string path);

    }
    internal class FileOperationsHelper : IFileOperationsHelper
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

        public void SetLastWriteTimeUtc(string path, DateTime utc)
            => File.SetLastWriteTimeUtc(path, utc);
        public FileAttributes GetAttributes(string path)
            => File.GetAttributes(path);

        public void SetAttributes(string path, FileAttributes attributes)
            => File.SetAttributes(path, attributes);

        public string Combine(string path1, string path2)
            => Path.Combine(path1, path2);

        public string GetRandomFileName()
            => Path.GetRandomFileName();

        public Stream CreateNewFile(string path)
            => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        public IEnumerable<string> EnumerateFileSystemEntries(string path)
            => Directory.EnumerateFileSystemEntries(path);
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions options)
        => Directory.EnumerateFileSystemEntries(path, searchPattern, options);

        public string NormalizePath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public bool DirectoriesAreNested(string path1, string path2)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            
            // Same directories are not allowed
            if (string.Equals(path1, path2, comparison))
                return true;

            // Can't be subdirectory
            if (path1.StartsWith(path2 + Path.DirectorySeparatorChar, comparison))
                return true;

            if (path2.StartsWith(path1 + Path.DirectorySeparatorChar, comparison))
                return true;

            return false;
        }

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

        
        public string GetRelativePath(string relativeTo, string path)
            => Path.GetRelativePath(relativeTo, path);


        public (long Length, DateTime LastWriteTimeUtc) GetFileSizeAndLastWriteTimeUtc(string path)
        {
            var fileMetadata = new FileInfo(path);
            return (fileMetadata.Length, fileMetadata.LastWriteTimeUtc);
        }
    }
}
