using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BFUlib
{
    public class TaskProcessor
    {
        struct Remote
        {
            public Location Location { get; }
            public IConnection Connection { get; }

            public Remote(Location loc, IConnection con)
            {
                Location = loc;
                Connection = con;
            }
        }

        private readonly Settings _settings;

        private readonly FileWatcher _fileWatcher;
        private readonly List<Remote> _targetList = new List<Remote>();

        public ChangeList Changes { get; }

        //TODO: probably should be just a private field
        private readonly ConcurrentDictionary<Guid, BfuTask> _queue = new ConcurrentDictionary<Guid, BfuTask>();

        public event Action<BfuTask> BfuTaskProcessed;

        public TaskProcessor(Settings settings)
        {
            if(!settings?.TargetList.Any() ?? false)
                throw new ArgumentException($"{nameof(Settings)} is null or no target defined");

            _settings = settings;

            foreach(var location in _settings.TargetList)
            {
                _targetList.Add(new Remote(location, Connection.Create(location)));
            }

            Changes = string.IsNullOrEmpty(settings.ChangeListPath) ? null : new ChangeList(settings.ChangeListPath);

            _fileWatcher = new FileWatcher(settings.LocalPath, settings.IgnorePatterns)
            { ExitRequestFile = settings.ExitRequestFile };
        }

        public async Task StartAsync()
        {
            if(_settings.AllowMultiThreadedUpload)
                await Task.WhenAll(_targetList.Select(x => x.Connection.ConnectAsync()));
            else
            {
                foreach(var connection in _targetList.Select(x => x.Connection))
                {
                    connection.Connect();
                }
            }

            _fileWatcher.Start();
        }

        public void Stop()
        {
            _fileWatcher.Stop();

            foreach(var connection in _targetList.Select(x => x.Connection))
            {
                connection.Disconnect();
            }
        }

        private string PrepareTargetPath(string path, Location location)
        {
            var tp = PortablePath.GenerateRemotePath(path, _settings.LocalPath, location.TargetPath);

            if (location.CreateTimestampedCopies)
                tp = $"{tp}_{DateTime.Now:yyyyMMddHHmmss}";

            return tp;
        }

        public async Task ProcessAsync()
        {
            bool exit = false;
            _fileWatcher.ExitRequested += () => exit = true;

            while(!exit)
            {
                if(_fileWatcher.Queue.TryDequeue(out var fileOperation))
                {
                    switch(fileOperation.Operation)
                    {
                        case Operation.Add:
                        case Operation.Change:
                            foreach(var r in _targetList)
                            {
                                var bt = new BfuTask(r.Connection, 
                                                     fileOperation.Path, 
                                                     PrepareTargetPath(fileOperation.Path, r.Location),
                                                     TaskFinished);
                                bt.Start();
                                _queue.TryAdd(bt.Guid, bt);   //it's a new GUID, should always work
                            }

                            break;

                        case Operation.Delete:
                            //Log($"Local delete of: {fileOperation.Path}");
                            break;

                        default:
                            //Log($"ERROR: {fileOperation.Operation} not implemented");
                            break;
                    }

                    continue;
                }

                var reTask = _queue.FirstOrDefault(x => x.Value.Status == CommandStatus.Pending).Value;
                if(reTask != null)
                {
                    reTask.Start();
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private void TaskFinished(BfuTask task)
        {
            int retries = 0;
            while(!_queue.TryRemove(task.Guid, out BfuTask tmp))
            {
                Task.Delay(TimeSpan.FromMilliseconds(100));
                if(retries++ > 10)
                {
                    Debug.WriteLine("Can not remove task from queue");
                    break;
                }
            }

            BfuTaskProcessed?.Invoke(task);

            if(task.Status == CommandStatus.Failed)
            {
                var newTask = new BfuTask(task);
                _queue.TryAdd(newTask.Guid, newTask);
            }
        }
    }
}
