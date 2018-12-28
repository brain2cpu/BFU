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
            var changes = string.IsNullOrEmpty(settings.ChangeListPath) ? null : new ChangeList(settings.ChangeListPath);

            var fileHandler = new FileHandler(settings);
            var fileWatcher = new FileWatcher(settings.LocalPath, settings.IgnorePatterns)
                {ExitRequestFile = ".exit"};

            try
            {
                await fileHandler.ConnectAllAsync();
                fileWatcher.Start();

                await Process(log, changes, fileHandler, fileWatcher);
                return 0;
            }
            catch(Exception xcp)
            {
                log.Write(xcp.Message);
                return 9;
            }
            finally
            {
                fileWatcher.Stop();
                fileHandler.DisconnectAll();
            }
        }

        private static async Task Process(Log log, ChangeList changes, FileHandler fileHandler, FileWatcher fileWatcher)
        {
            void Log(string msg) => Console.WriteLine(log.Write(msg));

            bool exit = false;
            fileWatcher.ExitRequested += () => exit = true;

            while(!exit)
            {
                if(fileWatcher.Queue.TryPeek(out var fileOperation))
                {
                    switch(fileOperation.Operation)
                    {
                        case Operation.Add:
                        case Operation.Change:
                            if(await fileHandler.UploadAsync(fileOperation.Path))
                            {
                                fileWatcher.Queue.TryDequeue(out fileOperation);

                                changes?.Add(fileOperation.Path);
                                Log($"Upload {fileOperation.Path}");
                            }
                            else
                            {
                                Log($"Upload failed {fileOperation.Path}{Environment.NewLine}{fileHandler.LastUploadErrors}");
                                await Task.Delay(TimeSpan.FromSeconds(1));
                            }
                            break;

                        case Operation.Delete:
                            fileWatcher.Queue.TryDequeue(out fileOperation);
                            Log($"Local delete of: {fileOperation.Path}");
                            break;

                        default:
                            Log($"ERROR: {fileOperation.Operation} not implemented");
                            break;
                    }
                }
                else
                    await Task.Delay(TimeSpan.FromSeconds(1));
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
