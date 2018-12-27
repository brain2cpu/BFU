using System.Collections.Generic;
using System.IO;

namespace BFUlib
{
    public class ChangeList
    {
        private readonly string _path;

        public ChangeList(string path)
        {
            _path = path;
        }

        public void Add(string file) 
        {
            Add(new[] { file });
        }

        public void Add(IEnumerable<string> files)
        {
            HashSet<string> changes = new HashSet<string>();

            if(File.Exists(_path))
            {
                using (var sr = new StreamReader(_path))
                {
                    while(!sr.EndOfStream)
                    {
                        changes.Add(sr.ReadLine());
                    }
                }
            }

            foreach(string file in files)
                changes.Add(file);

            using (var sw = new StreamWriter(_path))
            {
                foreach (string f in changes)
                    sw.WriteLine(f);
            }
        }

        public IEnumerable<string> Get()
        {
            var res = new List<string>();

            if (!File.Exists(_path))
                return res;

            using (var sr = new StreamReader(_path))
            {
                while (!sr.EndOfStream)
                {
                    res.Add(sr.ReadLine());
                }
            }

            return res;
        }
    }
}
