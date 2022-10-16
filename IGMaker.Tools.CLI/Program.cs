using System.Diagnostics;

namespace IGMaker.Tools.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IGArguments? arguments = args.Length > 1 ? new IGArguments(args) : null;

            Console.OutputEncoding = System.Text.Encoding.Unicode;
            Console.InputEncoding = System.Text.Encoding.Unicode;

            IGMakerProject proj;
            Stopwatch sw = new Stopwatch();
            TextWriter writer = Console.Out;
            Stream? logStrm = null;
            if (arguments == null)
            {
                arguments = new IGArguments();

                Console.Clear();
                Console.WriteLine($"Welcome to the IGMaker game extractor!");
                Console.WriteLine($"Enter the input path: ");
                arguments.inputPath = Console.ReadLine();

                Console.WriteLine($"Enter the output path (leave blank for automatic): ");
                arguments.outputPath = Console.ReadLine();

                Console.WriteLine("What paks would you like to extract");
                Console.WriteLine(" - Press '1' for Files");
                Console.WriteLine(" - Press '2' for Streamed");
                Console.WriteLine(" - Press '3' for All");
                FlushConsole();

                while (true)
                {
                    var key = Console.ReadKey(true).Key;
                    if(key == ConsoleKey.D1)
                    {
                        arguments.flags |= IGFlags.File;
                        break;
                    }

                    if (key == ConsoleKey.D2)
                    {
                        arguments.flags |= IGFlags.Stream;
                        break;
                    }

                    if (key == ConsoleKey.D3)
                    {
                        arguments.flags |= IGFlags.AllTypes;
                        break;
                    }
                }
                Console.WriteLine($"Selected types '{arguments.flags}'");
                Console.WriteLine("Would you like to group assets by their type (Y/N)");
                FlushConsole();

                while (true)
                {
                    var key = Console.ReadKey(true).Key;
                    if(key == ConsoleKey.Y)
                    {
                        arguments.flags |= IGFlags.GroupByType;
                        Console.WriteLine("Group by asset type!");
                        break;
                    }

                    if (key == ConsoleKey.N)
                    {
                        Console.WriteLine("Do not group by asset type!");
                        break;
                    }
                }

                Console.WriteLine("Log stuff to console? (Y/N)");
                FlushConsole();
                while (true)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Y)
                    {
                        arguments.logLevel = 5;
                        Console.WriteLine("Logging stuff to console");
                        break;
                    }

                    if (key == ConsoleKey.N)
                    {
                        arguments.logLevel = 0;
                        Console.WriteLine("No logging");
                        break;
                    }
                }

                Console.WriteLine("Press enter to begin!");
                FlushConsole();
                while (true)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter)
                    {
                        Console.Clear();
                        break;
                    }
                }
            }

            if(!string.IsNullOrWhiteSpace(arguments?.logPath))
            {
                logStrm = new FileStream(arguments.logPath, FileMode.Create);
                writer = new StreamWriter(logStrm);
            }

            proj = new IGMakerProject(arguments.inputPath, writer);
            proj.OutputPath = arguments.outputPath;
            proj.LogLevel = arguments.logLevel;
            proj.LogFrequency = arguments.logFrequency;

            sw.Start();
            var res = proj.FindPaks(arguments.flags);
            if (res != IGResult.Success) 
            {
                writer.WriteLine($"PAK finding failed! [{res}] \n{proj.LastException}");
                if (logStrm != null)
                {
                    writer.Dispose();
                    logStrm.Dispose();
                }
                return;
            }

            res = proj.ReadAssets(arguments.flags);
            if (res != IGResult.Success)
            {
                writer.WriteLine($"Asset reading failed! [{res}] \n{proj.LastException}");
                if (logStrm != null)
                {
                    writer.Dispose();
                    logStrm.Dispose();
                }
                return;
            }
            res = proj.ExtractAssets(arguments.flags);
            if (res != IGResult.Success)
            {
                writer.WriteLine($"Asset extraction failed! [{res}] \n{proj.LastException}");
                if (logStrm != null)
                {
                    writer.Dispose();
                    logStrm.Dispose();
                }
                return;
            }
            sw.Stop();

            writer.WriteLine($"\nElapsed time: {sw.Elapsed.TotalSeconds:F4} sec, {sw.ElapsedMilliseconds} ms, {sw.ElapsedTicks}");
            if(logStrm != null)
            {
                writer.Dispose();
                logStrm.Dispose();
            }
        }

        private static void FlushConsole()
        {
            while (Console.KeyAvailable)
            {
                Console.ReadKey(true);
            }
        }
    }
}