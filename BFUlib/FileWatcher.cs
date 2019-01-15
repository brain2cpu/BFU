using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using static BFUlib.PortablePath;

namespace BFUlib
{
    public class FileWatcher
    {
        //a file with this name (if set) created in the root of watched directory will raise the ExitRequested event and delete the new file
        public string ExitRequestFile { get; set; } = "";

        public event Action ExitRequested;

        private readonly string _directory;
        private readonly IgnorePatterns _ignorePatterns;

        private readonly FileSystemWatcher _watcher;

        public ConcurrentQueue<FileOperation> Queue { get; } = new ConcurrentQueue<FileOperation>();

        public FileWatcher(string directory, IgnorePatterns ignoredPatterns)
        {
            _directory = directory;
            _ignorePatterns = ignoredPatterns;

            _watcher = new FileSystemWatcher(_directory, "*.*")
            {
                IncludeSubdirectories = true
            };
            _watcher.Changed += _watcher_Changed;
            _watcher.Created += _watcher_Created;
            _watcher.Deleted += _watcher_Deleted;
            _watcher.Renamed += _watcher_Renamed;
        }

        void _watcher_Renamed(object sender, RenamedEventArgs e) => HandleNewFile(e.FullPath);

        private void _watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(Ignore(e.FullPath))
                return;

            if(Queue.Any(x => string.Equals(x.Path, e.FullPath) && (x.Operation == Operation.Add || x.Operation == Operation.Change)))
                return;

            Queue.Enqueue(new FileOperation { Path = e.FullPath, Operation = Operation.Change });
        }

        private void _watcher_Created(object sender, FileSystemEventArgs e) => HandleNewFile(e.FullPath);

        private void HandleNewFile(string path)
        {
            if (!string.IsNullOrEmpty(ExitRequestFile) &&
                           SameDirectory(Path.GetDirectoryName(path), _directory, true) &&
                           SameFilename(Path.GetFileName(path), ExitRequestFile, true))
            {
                File.Delete(path);
                ExitRequested?.Invoke();
                return;
            }

            if (Ignore(path))
                return;

            if (Queue.Any(x => string.Equals(x.Path, path) && (x.Operation == Operation.Add || x.Operation == Operation.Change)))
                return;

            Queue.Enqueue(new FileOperation { Path = path, Operation = Operation.Add });
        }

        private void _watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if(Ignore(e.FullPath))
                return;

            Queue.Enqueue(new FileOperation { Path = e.FullPath, Operation = Operation.Delete });
        }

        private bool Ignore(string path) => _ignorePatterns?.ShouldIgnore(path) ?? false;

        public void Start() => _watcher.EnableRaisingEvents = true;

        public void Stop() => _watcher.EnableRaisingEvents = false;
    }
}
