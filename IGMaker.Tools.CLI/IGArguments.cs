namespace IGMaker.Tools.CLI
{
    internal class IGArguments
    {
        public string inputPath = "";
        public string outputPath = "";

        public IGFlags flags = IGFlags.None;

        public string logPath = "";
        public int logLevel = 5;
        public int logFrequency = 0;

        public IGArguments() { }
        public IGArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var str = args[i];
                bool isLast = i >= args.Length - 1;
                int val;
                switch (str)
                {
                    case "-i":
                        if (isLast) { break; }
                        inputPath = Path.GetFullPath(args[++i]);
                        break;
                    case "-o":
                        if (isLast) { break; }
                        outputPath = Path.GetFullPath(args[++i]);
                        break;
                    case "-g":
                        flags |= IGFlags.GroupByType;
                        break;
                    case "-f":
                        flags |= IGFlags.File;
                        break;
                    case "-s":
                        flags |= IGFlags.Stream;
                        break;

                    case "-lp":
                        if (isLast) { break; }
                        logPath = Path.GetFullPath(args[++i]);
                        break;
                    case "-ll":
                        if (isLast) { break; }
                        logLevel = int.TryParse(args[++i], out val) ? val : 0;
                        break;
                    case "-lf":
                        if (isLast) { break; }
                        logFrequency = int.TryParse(args[++i], out val) ? val : 0;
                        break;
                }
            }
        }
    }
}