using System;
using System.IO;

namespace BFUlib
{
    public class Log
    {
        private readonly object lockObj = new object();

        private readonly string _path;

        public Log(string path) => _path = path;

        public string Write(string msg)
        {
            string s = $"{DateTime.UtcNow:yyyy.MM.dd HH:mm:ss} UTC - {msg}{Environment.NewLine}";

            if(!string.IsNullOrEmpty(_path))
            {
                lock(lockObj)
                {
                    using(var sw = new StreamWriter(_path, true))
                    {
                        sw.WriteLine(s);
                    }
                }
            }

            return s;
        }

        public string Write(Exception xcp) => Write(xcp.Message);
    }
}
