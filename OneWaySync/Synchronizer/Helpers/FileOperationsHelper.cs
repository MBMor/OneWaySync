
namespace OneWaySync.Synchronizer.Helpers
{
    public interface IFileOperationsHelper
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive);

        bool FileExists(string path);
        void CopyFile(string src, string dst, bool overwrite);
        void DeleteFile(string path);

        void SetLastWriteTimeUtc(string path, DateTime utc);
        void SetAttributes(string path, FileAttributes attributes);
        string Combine(string a, string b);
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

        public void SetAttributes(string path, FileAttributes attributes)
            => File.SetAttributes(path, attributes);

        public string Combine(string a, string b)
            => Path.Combine(a, b);

    }
}
