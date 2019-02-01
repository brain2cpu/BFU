using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentFTP;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace BFUlib
{
    public interface IConnection : IDisposable
    {
        void Connect();
        Task ConnectAsync();
        void Disconnect();
        void Upload(string path, string targetPath);
        Task UploadAsync(string path, string targetPath);
    }

    abstract class Connection : IConnection
    {
        protected readonly Location _location = null;

        protected Connection(Location location) => _location = location;

        public void Connect()
        {
            if(_location == null)
                throw new InvalidOperationException($"{nameof(Connect)} must be called first");

            try
            {
                DoConnect();
            }
            catch(Exception xcp)
            {
                throw new Exception($"Connection failed {_location.Name}{Environment.NewLine}{xcp.Message}", xcp);
            }
        }

        public async Task ConnectAsync()
        {
            await Task.Run(() => Connect());
        }

        protected abstract void DoConnect();

        public void Disconnect()
        {
            try
            {
                DoDisconnect();
            }
            catch(Exception xcp)
            {
                Debug.WriteLine(xcp.Message);
            }
        }

        protected abstract void DoDisconnect();

        protected abstract bool IsConnected();

        public void Upload(string path, string targetPath)
        {
            if(!IsConnected())
            {
                Disconnect();
                Connect();
            }

            try
            {
                DoUpload(path, targetPath);
            }
            catch(Exception xcp)
            {
                throw new BFUException(_location, xcp);
            }
        }

        public async Task UploadAsync(string path, string targetPath)
        {
            await Task.Run(() => Upload(path, targetPath));
        }

        protected abstract void DoUpload(string path, string targetPath);

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

        protected override void DoUpload(string path, string targetPath)
        {
            string dir = Path.GetDirectoryName(targetPath);
            if(string.IsNullOrEmpty(dir))
                throw new ArgumentException($"Directory must be specified for {nameof(targetPath)}={targetPath}");

            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if(File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                if (fi.IsReadOnly)
                    fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
            }

            File.Copy(path, targetPath, true);
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

        protected override void DoUpload(string path, string targetPath)
        {
            _client.UploadFile(path, targetPath, FtpExists.Overwrite, true, FtpVerify.Retry);
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

        protected override void DoUpload(string path, string targetPath)
        {
            try
            {
                UploadAndIgnoreSetTime(path, targetPath);
            }
            catch(ScpException xcp)
            {
                if(xcp.Message.ToLowerInvariant().Contains("no such file or directory"))
                {
                    CreateDirectory(Path.GetDirectoryName(targetPath));
                    UploadAndIgnoreSetTime(path, targetPath);
                }
                else if(xcp.Message.ToLowerInvariant().Contains("permission denied"))
                {
                    ChangeRights(targetPath);
                    UploadAndIgnoreSetTime(path, targetPath);
                }
                else
                    throw;
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }
        }

        private void UploadAndIgnoreSetTime(string path, string targetPath)
        {
            try
            {
                _client.Upload(new FileInfo(path), targetPath);
            }
            //suddenly this version leads to a segfault on Mac, turn to the classic approach
            //catch(ScpException scpXcp) when(scpXcp.Message.ToLowerInvariant().Contains("set times: Operation not permitted"))
            //{
            //    Debug.WriteLine($"Ignore: {scpXcp.Message}");
            //}
            catch(ScpException xcp)
            {
                if(xcp.Message.ToLowerInvariant().Contains("set times: operation not permitted"))
                    Debug.WriteLine($"Ignore: {xcp.Message}");
                else
                    throw;
            }

            try
            {
                ReconnectCmdIfNeeded();
                foreach(var cmd in _location.Commands)
                {
                    if(cmd.MatchingFile != null && !cmd.MatchingFile.IsMatch(targetPath))
                        continue;

                    string cmdStr = string.Format(cmd.Cmd, targetPath);
                    var r = _cmdClient.RunCommand(cmdStr);
                    Console.WriteLine($"{cmdStr}: {r.ExitStatus} {r.Result}");
                }
            }
            catch(Exception xcp)
            {
                Console.WriteLine(xcp.Message);
            }
        }

        private void CreateDirectory(string dir)
        {
            ReconnectCmdIfNeeded();

            //the sudo version needs the following line added to /etc/sudoers with visudo
            //MYUSER   ALL = NOPASSWD: /bin/mkdir
            _cmdClient.RunCommand((_location.UseSudoInCmds ? "sudo " : "") + $"mkdir -p {dir}");
        }

        private void ChangeRights(string path)
        {
            ReconnectCmdIfNeeded();

            //the sudo version needs the following lines added to /etc/sudoers with visudo
            //MYUSER   ALL = NOPASSWD: /bin/touch
            //MYUSER   ALL = NOPASSWD: /bin/chmod
            _cmdClient.RunCommand((_location.UseSudoInCmds ? "sudo " : "") + $"touch {path}");
            _cmdClient.RunCommand((_location.UseSudoInCmds ? "sudo " : "") + $"chmod a+rw {path}");
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
