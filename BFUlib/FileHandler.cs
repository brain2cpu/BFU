using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFUlib
{
    public class FileHandler
    {
        private readonly Settings _settings;

        private readonly Dictionary<Guid, IConnection> _targetList = new Dictionary<Guid, IConnection>();

        public FileHandler(Settings settings)
        {
            _settings = settings;

            if(_settings == null || !_settings.TargetList.Any())
                throw new ArgumentException("No target defined in settings");

            foreach(var location in _settings.TargetList)
            {
                _targetList.Add(location.Id, Connection.Create(location));
            }
        }

        public async Task ConnectAllAsync()
        {
            if(_settings.AllowMultiThreadedUpload)
                await Task.WhenAll(_targetList.Values.Select(x => x.ConnectAsync()));
            else
            {
                foreach(var connection in _targetList.Values)
                {
                    connection.Connect();
                }
            }
        }

        public void DisconnectAll()
        {
            foreach(var connection in _targetList.Values)
            {
                connection.Disconnect();
            }
        }

        private string PrepareTargetPath(string path, Guid id)
        {
            var location = _settings.TargetList.Single(x => x.Id == id);

            var tp = PortablePath.GenerateRemotePath(path, _settings.LocalPath, location.TargetPath);

            if(location.CreateTimestampedCopies)
                tp = $"{tp}_{DateTime.Now:yyyyMMddHHmmss}";

            return tp;
        }

        public string LastUploadErrors { get; private set; } = "";

        private async Task<(bool, string)> TryUploadAsync(string path, KeyValuePair<Guid, IConnection> x)
        {
            string tp = PrepareTargetPath(path, x.Key);
            try
            {
                await x.Value.UploadAsync(path, tp);
                return (true, "");
            }
            catch(Exception xcp)
            {
                return (false, $"Upload of {tp} failed {xcp.Message}");
            }
        }

        private (bool, string) TryUpload(string path, KeyValuePair<Guid, IConnection> x)
        {
            string tp = PrepareTargetPath(path, x.Key);
            try
            {
                x.Value.Upload(path, tp);
                return (true, "");
            }
            catch(Exception xcp)
            {
                return (false, $"Upload of {tp} failed {xcp.Message}");
            }
        }

        public async Task<bool> UploadAsync(string path)
        {
            LastUploadErrors = "";
            bool res = true;
            var err = new StringBuilder();

            if(_settings.AllowMultiThreadedUpload && _settings.TargetList.Count(x => x.Method != CopyMethod.Copy) > 1)
            {
                foreach(var (isSuccess, error) in await Task.WhenAll(_targetList.Select(x => TryUploadAsync(path, x))))
                {
                    if(!isSuccess)
                    {
                        res = false;
                        err.AppendLine(error);
                    }
                }

                LastUploadErrors = err.ToString();
                return res;
            }

            foreach(var kv in _targetList)
            {
                var(isSuccess, error) = TryUpload(path, kv);
                if(!isSuccess)
                {
                    res = false;
                    err.AppendLine(error);
                }
            }

            LastUploadErrors = err.ToString();
            return res;
        }
    }
}
