using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace BFUlib
{
    public interface IConnection : IDisposable
    {
        MessageList Connect();
        Task<MessageList> ConnectAsync();
        MessageList Disconnect();
        MessageList Upload(string path, string targetPath);
        Task<MessageList> UploadAsync(string path, string targetPath);
    }

    abstract class Connection : IConnection
    {
        protected readonly Location _location = null;

        protected Connection(Location location) => _location = location;

        public MessageList Connect()
        {
            if(_location == null)
                throw new NullReferenceException("location can not be null");

            try
            {
                DoConnect();
                return Message.Info($"Connected to {_location.Name}");
            }
            catch(Exception xcp)
            {
                return Message.Error($"Connection to {_location.Name} failed", xcp);
            }
        }

        public async Task<MessageList> ConnectAsync()
        {
            return await Task.Run(() => Connect());
        }

        protected abstract void DoConnect();

        public MessageList Disconnect()
        {
            try
            {
                DoDisconnect();
                return Message.Info($"Disconnected from {_location.Name}");
            }
            catch(Exception xcp)
            {
                return Message.Error($"Error disconnecting from {_location.Name}", xcp);
            }
        }

        protected abstract void DoDisconnect();

        protected abstract bool IsConnected();

        public MessageList Upload(string path, string targetPath)
        {
            if(!IsConnected())
            {
                Disconnect();

                var msg = Connect();
                if(!msg.IsSuccess)
                    return msg;
            }

            try
            {
                return DoUpload(path, targetPath);
            }
            catch(Exception xcp)
            {
                return Message.Error($"Error uploading {path} to {_location.Name}:{targetPath}", xcp);
            }
        }

        public async Task<MessageList> UploadAsync(string path, string targetPath)
        {
            return await Task.Run(() => Upload(path, targetPath));
        }

        protected abstract MessageList DoUpload(string path, string targetPath);

        public void Dispose() => Disconnect();

        public static IConnection Create(Location location)
        {
            switch(location.Method)
            {
                case CopyMethod.Copy:
                    return new LocalConnection(location);

                case CopyMethod.Ftp:
                    return new FtpConnection(location);

                case CopyMethod.Scp:
                    return new ScpConnection(location);

                default:
                    throw new InvalidOperationException($"Unknown method {location.Method}");
            }
        }
    }

    class LocalConnection : Connection
    {
        public LocalConnection(Location location) : base(location)
        {
            if(location.Method != CopyMethod.Copy)
                throw new InvalidOperationException($"{nameof(LocalConnection)} supports {CopyMethod.Copy} only");
        }

        protected override void DoConnect()
        {
        }

        protected override void DoDisconnect()
        {
        }

        protected override bool IsConnected() => true;

        protected override MessageList DoUpload(string path, string targetPath)
        {
            string dir = Path.GetDirectoryName(targetPath);
            if(string.IsNullOrEmpty(dir))
                throw new ArgumentException($"Directory must be specified for {nameof(targetPath)}={targetPath}");

            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if(File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                if(fi.IsReadOnly)
                    fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
            }

            File.Copy(path, targetPath, true);

            return Message.Info($"{path} copied to {targetPath}");
        }
    }

    class FtpConnection : Connection
    {
        private FtpClient _client = null;

        public FtpConnection(Location location) : base(location)
        {
            if(location.Method != CopyMethod.Ftp)
                throw new InvalidOperationException($"{nameof(LocalConnection)} supports {CopyMethod.Ftp} only");
        }

        protected override void DoConnect()
        {
            _client = new FtpClient(_location.Host, _location.Port, _location.Username, _location.Password);
            _client.Connect();
        }

        protected override void DoDisconnect()
        {
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
        }

        protected override bool IsConnected() => _client?.IsConnected == true;

        protected override MessageList DoUpload(string path, string targetPath)
        {
            _client.UploadFile(path, targetPath, FtpExists.Overwrite, true, FtpVerify.Retry);

            return Message.Info($"{path} uploaded to {_location.Name}:{targetPath}");
        }
    }

    //cmdClient is connected only if it is required
    class ScpConnection : Connection
    {
        private ScpClient _client = null;
        private SshClient _cmdClient = null;

        public ScpConnection(Location location) : base(location)
        {
            if(location.Method != CopyMethod.Scp)
                throw new InvalidOperationException($"{nameof(LocalConnection)} supports {CopyMethod.Scp} only");
        }

        protected override void DoConnect()
        {
            _client = new ScpClient(_location.Host, _location.Port, _location.Username, _location.Password);
            _client.Connect();
        }

        protected override void DoDisconnect()
        {
            //Disconnect hangs
            //https://github.com/sshnet/SSH.NET/issues/355
            //so live with leaks for now

            /*
            _client?.Disconnect();
            _cmdClient?.Disconnect();

            _client?.Dispose();
            _cmdClient?.Dispose();
            */

            _client = null;
            _cmdClient = null;
        }

        protected override bool IsConnected() => _client?.IsConnected == true;

        protected override MessageList DoUpload(string path, string targetPath)
        {
            var ml = new MessageList();

            try
            {
                return UploadAndIgnoreSetTime(path, targetPath);
            }
            catch(ScpException xcp)
            {
                if(xcp.Message.ToLowerInvariant().Contains("no such file or directory"))
                {
                    ml.Add(CreateDirectory(Path.GetDirectoryName(targetPath)));
                    ml.Add(UploadAndIgnoreSetTime(path, targetPath));
                }
                else if(xcp.Message.ToLowerInvariant().Contains("permission denied"))
                {
                    ml.Add(ChangeRights(targetPath));
                    ml.Add(UploadAndIgnoreSetTime(path, targetPath));
                }
                else
                {
                    ml.Add(Message.Error($"Error uploading {path} to {_location.Name}:{targetPath}", xcp));
                }
            }
            catch(Exception e)
            {
                ml.Add(Message.Error($"Error uploading {path} to {_location.Name}:{targetPath}", e));
            }

            return ml;
        }

        private MessageList UploadAndIgnoreSetTime(string path, string targetPath)
        {
            var ml = new MessageList();

            try
            {
                _client.Upload(new FileInfo(path), targetPath);
                ml.Add(Message.Info($"{path} uploaded to {_location.Name}:{targetPath}"));
            }
            //suddenly this version leads to a segfault on Mac, turn to the classic approach
            //catch(ScpException scpXcp) when(scpXcp.Message.ToLowerInvariant().Contains("set times: Operation not permitted"))
            //{
            //    Debug.WriteLine($"Ignore: {scpXcp.Message}");
            //}
            catch(ScpException xcp)
            {
                if(xcp.Message.ToLowerInvariant().Contains("set times: operation not permitted"))
                {
                    ml.Add(Message.Info($"{path} uploaded to {_location.Name}:{targetPath}"));
                    Debug.WriteLine($"Ignore: {xcp.Message}");
                }
                else
                    throw;
            }

            if(_location.Commands.Any())
            {
                try
                {
                    ReconnectCmdIfNeeded();
                    foreach(var cmd in _location.Commands)
                    {
                        if(cmd.MatchingFile != null && !cmd.MatchingFile.IsMatch(targetPath))
                            continue;

                        string cmdStr = string.Format(cmd.Cmd, targetPath);
                        var r = _cmdClient.RunCommand(cmdStr);
                        ml.Add(Message.Info($"{cmdStr}: {r.ExitStatus} {r.Result}"));
                    }
                }
                catch(Exception xcp)
                {
                    ml.Add(Message.Error("Commands failed", xcp));
                }
            }

            return ml;
        }

        private MessageList CreateDirectory(string dir)
        {
            ReconnectCmdIfNeeded();

            //the sudo version needs the following line added to /etc/sudoers with visudo
            //MYUSER   ALL = NOPASSWD: /bin/mkdir
            var cmdStr = (_location.UseSudoInCmds ? "sudo " : "") + $"mkdir -p {dir}";
            var r = _cmdClient.RunCommand(cmdStr);

            return Message.Info($"{cmdStr}: {r.ExitStatus} {r.Result}");
        }

        private MessageList ChangeRights(string path)
        {
            ReconnectCmdIfNeeded();

            var ml = new MessageList();

            //the sudo version needs the following lines added to /etc/sudoers with visudo
            //MYUSER   ALL = NOPASSWD: /bin/touch
            //MYUSER   ALL = NOPASSWD: /bin/chmod
            var cmdStr = (_location.UseSudoInCmds ? "sudo " : "") + $"touch {path}";
            _cmdClient.RunCommand(cmdStr);
            var r = _cmdClient.RunCommand(cmdStr);
            ml.Add(Message.Info($"{cmdStr}: {r.ExitStatus} {r.Result}"));

            cmdStr = (_location.UseSudoInCmds ? "sudo " : "") + $"chmod a+rw {path}";
            r = _cmdClient.RunCommand(cmdStr);
            ml.Add(Message.Info($"{cmdStr}: {r.ExitStatus} {r.Result}"));

            return ml;
        }

        private void ReconnectCmdIfNeeded()
        {
            if(_cmdClient?.IsConnected != true)
            {
                _cmdClient = new SshClient(_location.Host, _location.Port, _location.Username, _location.Password);
                _cmdClient.Connect();
            }
        }
    }
}
