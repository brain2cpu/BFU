using BFUlib;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BFU
{
    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            if(args.Length == 0 || string.IsNullOrEmpty(args[0]) || !File.Exists(args[0]))
            {
                UsageReport();
                return 1;
            }

            Settings settings;
            try
            {
                settings = Settings.Load(args[0]);
            }
            catch(Exception xcp)
            {
                Console.WriteLine($"Invalid settings file {args[0]}{Environment.NewLine}{xcp.Message}");
                return 2;
            }

            if(string.IsNullOrEmpty(settings.LocalPath) || !Directory.Exists(settings.LocalPath))
            {
                Console.WriteLine($"{nameof(settings.LocalPath)} {settings.LocalPath} does not exist");
                return 3;
            }

            var log = new Log(settings.LogPath);

            var tp = new TaskProcessor(settings);
            try
            {
                await tp.StartAsync();
                Console.WriteLine("Connected.");
                await tp.ProcessAsync();

                return 0;
            }
            catch(Exception xcp)
            {
                log.Write(xcp.Message);
                Console.WriteLine($"Fatal error: {xcp.Message}");
                return 9;
            }
            finally
            {
                tp.Stop();
            }
        }

        private static void UsageReport()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("dotnet BFU.dll settings.json");

            if(Settings.TryGenerateExampleFile("example_settings.json"))
                Console.WriteLine("example_settings.json was generated in the current directory");
        }
    }
}
