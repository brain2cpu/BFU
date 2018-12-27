using System;
using System.IO;
using System.Text.RegularExpressions;

namespace BFUlib
{
    static class PortablePath
    {
        private static readonly string _localDirSeparator = Path.DirectorySeparatorChar.ToString();

        private static readonly StringComparison _localPathComparison =
            Environment.OSVersion.Platform == PlatformID.Win32NT
                ? StringComparison.InvariantCultureIgnoreCase
                : StringComparison.InvariantCulture;

        private static readonly Regex _startsWithSlash = new Regex(@"^/");
        private static readonly Regex _startsWithDrive = new Regex(@"^[a-z]:", RegexOptions.IgnoreCase);
        private static readonly Regex _hasSlash = new Regex(@"/");
        private static readonly Regex _hasBackslash = new Regex(@"\\");
        private static readonly Regex _endsWithSlash = new Regex(@"/$");
        private static readonly Regex _endsWithBackslash = new Regex(@"\\$");

        public static string AppendSeparatorIfNeeded(string path)
        {
            if(string.IsNullOrEmpty(path))
                throw new ArgumentException("Path can not be null");

            if(_hasSlash.IsMatch(path))
                return _endsWithSlash.IsMatch(path) ? path : $"{path}/";

            if(_hasBackslash.IsMatch(path) || _startsWithDrive.IsMatch(path))
                return _endsWithBackslash.IsMatch(path) ? path : $"{path}\\";

            throw new ArgumentException($"Path {path} seems to be a filename");
        }

        public static string AppendLocalSeparatorIfNeeded(string path)
        {
            if(string.IsNullOrEmpty(path))
                throw new ArgumentException("Path can not be null");

            return path.EndsWith(_localDirSeparator) ? path : $"{path}{_localDirSeparator}";
        }

        public static bool IsFullPath(string path)
        {
            if(string.IsNullOrEmpty(path))
                return false;

            return _startsWithSlash.IsMatch(path) || _startsWithDrive.IsMatch(path);
        }

        public static bool SameDirectory(string d1, string d2, bool local)
        {
            if(string.IsNullOrEmpty(d1) || string.IsNullOrEmpty(d2))
                return false;

            return string.Equals(AppendSeparatorIfNeeded(d1), AppendSeparatorIfNeeded(d2),
                local ? _localPathComparison : StringComparison.Ordinal);
        }

        public static bool SameFilename(string f1, string f2, bool local)
        {
            if(string.IsNullOrEmpty(f1) || string.IsNullOrEmpty(f2))
                return false;

            return string.Equals(f1, f2, local ? _localPathComparison : StringComparison.InvariantCulture);
        }

        private static string ExtractSeparator(string path)
        {
            if(_hasSlash.IsMatch(path))
                return "/";

            if(_hasBackslash.IsMatch(path))
                return "\\";

            throw new ArgumentException($"{path} does not contain directory separator");
        }

        public static string GenerateRemotePath(string localPath, string localRoot, string remoteRoot)
        {
            if(string.IsNullOrEmpty(localPath))
                throw new ArgumentException("Path can not be null");

            string ls = ExtractSeparator(localRoot);
            string rs = ExtractSeparator(remoteRoot);

            if(ls == rs)
                return localPath.Replace(localRoot, remoteRoot);

            return localPath.Replace(localRoot, remoteRoot).Replace(ls, rs);
        }
    }
}
