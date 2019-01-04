using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BFUlib
{
    public enum IgnorePatternType { Directory, File }

    public class IgnorePattern
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public IgnorePatternType PatternType { get; set; } = IgnorePatternType.Directory;
        public Regex Regex { get; set; }

        public static IgnorePattern DefaultDirectoryIgnorePattern => new IgnorePattern {Regex = new Regex(@"[/\\]\.")};

        public static IgnorePattern DefaultFileIgnorePattern => new IgnorePattern
            {PatternType = IgnorePatternType.File, Regex = new Regex(@"^\.")};
    }

    public class IgnorePatterns
    {
        public List<IgnorePattern> Patterns { get; } = new List<IgnorePattern>();

        public void AddDefaultPatterns()
        {
            Patterns.Add(IgnorePattern.DefaultDirectoryIgnorePattern);
            Patterns.Add(IgnorePattern.DefaultFileIgnorePattern);
        }

        public bool ShouldIgnore(string path)
        {
            if(string.IsNullOrEmpty(path))
                throw new ArgumentException("Path can not be empty");

            if(!Patterns.Any())
                return false;

            string dir = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            foreach(var ignorePattern in Patterns)
            {
                if(ignorePattern.PatternType == IgnorePatternType.Directory)
                {
                    if(!string.IsNullOrEmpty(dir) && ignorePattern.Regex.IsMatch(dir))
                        return true;
                }
                else
                {
                    if(ignorePattern.Regex.IsMatch(file))
                        return true;
                }
            }

            return false;
        }
    }
}
