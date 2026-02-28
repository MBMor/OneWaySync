using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWaySync.GlobalHelpers
{
    public interface IPathService
    {
        string GetRandomFileName();
        string Combine(string path1, string path2);

        string GetRelativePath(string relativeTo, string path);
        string NormalizePath(string path);
        bool DirectoriesAreNested(string path1, string path2);
    }
    internal class PathService : IPathService
    {

        public string Combine(string path1, string path2)
            => Path.Combine(path1, path2);

        public string GetRandomFileName()
            => Path.GetRandomFileName();

        public string GetRelativePath(string relativeTo, string path)
            => Path.GetRelativePath(relativeTo, path);

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
    }    
}
