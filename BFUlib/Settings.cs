using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static BFUlib.PortablePath;

namespace BFUlib
{
    public enum CopyMethod { Copy, Ftp, Scp }

    public class CommandAfterUpload
    {
        public string Cmd { get; set; }
        public Regex MatchingFile { get; set; } = null;
    }

    public class Location
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public CopyMethod Method { get; set; } = CopyMethod.Copy;

        private string _name;
        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? $"{Method} {Host}:{Port}" : _name;
            set => _name = value;
        }

        public string Username { get; set; }
        public string Password { get; set; }

        public string Host { get; set; } = "localhost";
        public int Port { get; set; }

        private string _targetPath;
        public string TargetPath 
        { 
            get => _targetPath;
            set => _targetPath = string.IsNullOrEmpty(value) ? "" : AppendSeparatorIfNeeded(value);
        }

        public bool CreateTimestampedCopies { get; set; } = false;

        //valid only if Method == Scp 
        public bool UseSudoInCmds { get; set; } = false;

        private Guid? _id = null;
        public Guid Id
        {
            get
            {
                if(_id.HasValue && _id != Guid.Empty)
                    return _id.Value;

                _id = Guid.NewGuid();
                return _id.Value;
            }
            set => _id = value;
        }

        public List<CommandAfterUpload> Commands { get; } = new List<CommandAfterUpload>();
    }

    public class Settings
    {
        public const int DefaultFtpPort = 21;
        public const int DefaultScpPort = 22;

        public bool AllowMultiThreadedUpload { get; set; } = true;

        public List<Location> TargetList { get; } = new List<Location>();

        public IgnorePatterns IgnorePatterns { get; } = new IgnorePatterns();

        private string _localPath;
        public string LocalPath
        {
            get => _localPath;
            set => _localPath = string.IsNullOrEmpty(value) ? "" : AppendLocalSeparatorIfNeeded(value);
        }

        public string LogPath { get; set; }
        public string ChangeListPath { get; set; }

        public static Settings Load(string path)
        {
            using(var sr = new StreamReader(path))
            {
                return JsonConvert.DeserializeObject<Settings>(sr.ReadToEnd());
            }
        }

        public void Save(string path)
        {
            using(var sw = new StreamWriter(path))
            {
                sw.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public static bool TryGenerateExampleFile(string path)
        {
            try
            {
                string file = Path.GetFullPath(path);

                var settingsEx = new Settings
                {
                    LocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Development"),
                    LogPath = Path.ChangeExtension(file, ".log"),
                    ChangeListPath = Path.ChangeExtension(file, ".lst")
                };
                settingsEx.IgnorePatterns.AddDefaultPatterns();

                Location loc1 = new Location
                {
                    Name = "My SCP host",
                    Method = CopyMethod.Scp,
                    Host = "ssh.server.com",
                    Port = DefaultScpPort,
                    Username = "devel",
                    Password = "devel-pass",
                    TargetPath = "/usr/local/sf/",
                    UseSudoInCmds = true
                };
                loc1.Commands.Add(new CommandAfterUpload
                {
                    Cmd = "chmod a+x {0}",
                    MatchingFile = new Regex(@"\.(pl|cgi)$")
                });
                loc1.Commands.Add(new CommandAfterUpload
                {
                    Cmd = "sudo /etc/init.d/apache restart"
                });
                settingsEx.TargetList.Add(loc1);

                settingsEx.TargetList.Add(new Location
                {
                    Method = CopyMethod.Ftp,
                    Host = "ftp.server.com",
                    Port = DefaultFtpPort,
                    Username = "designer",
                    Password = "mypass",
                    TargetPath = "/home/www/html/"
                });

                settingsEx.TargetList.Add(new Location
                {
                    Name = "Local timestamped copy",
                    Method = CopyMethod.Copy,
                    TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Backup"),
                    CreateTimestampedCopies = true
                });

                settingsEx.Save(file);
            }
            catch(Exception xcp)
            {
                Debug.WriteLine(xcp.ToString());
                return false;
            }

            return true;
        }
    }
}
