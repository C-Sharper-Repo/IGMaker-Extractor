using System.Runtime.InteropServices;
using System.Text;

namespace IGMaker.Tools
{
    public class IGMakerProject
    {
        private static ReadOnlyMemory<byte>[] HEADERS = new ReadOnlyMemory<byte>[]
        {
            new byte[] { 0x4D ,0x4F ,0x44 ,0x55 ,0x4C ,0x45 ,0x5F ,0x53 ,0x45 ,0x5F ,0x5F ,0x5F ,0x5F ,0x5F ,0x5F ,0x5F }, //WAVE
            new byte[] { 0x4D ,0x4F ,0x44 ,0x55 ,0x4C ,0x45 ,0x5F ,0x57 ,0x41 ,0x4C ,0x4C ,0x5F ,0x5F ,0x5F ,0x5F ,0x5F }, //WALL
        };

        public const string STREAM_HEADER = "ACTKOOL_STRMHEAD";
        public const string FILES_HEADER = "ACTKOOL_FILEHEAD";

        private static Func<FileInfo, FileInfo, bool> DUPE_PRED = (FileInfo fA, FileInfo FB) => { return fA.FullName.Equals(FB.FullName, StringComparison.InvariantCultureIgnoreCase); };
        public const string STREAM_EXT = ".actstr";
        public const string FILE_EXT = ".actbin";

        public int LogLevel { get => _logLevel; set => _logLevel = Math.Clamp(value, 0, 5); }
        public int LogFrequency { get => _logFrequency; set => _logFrequency = Math.Max(value, 0); }

        public string? OutputPath
        {
            get => _outputDir;
            set
            {
                _outputDir = value?.Replace("\"", "");
                if (Path.HasExtension(_outputDir))
                {
                    _outputDir = Path.GetDirectoryName(_outputDir);
                }
            }
        }

        public string? RootPath
        {
            get => _path;
            set
            {
                _path = value?.Replace("\"", "");
                if (Path.HasExtension(_path))
                {
                    _path = Path.GetDirectoryName(_path);
                }
            }
        }

        public Exception? LastException => _lastEx;

        public TextWriter? Logger { get => _logger; set => _logger = value; }

        private Exception? _lastEx = null;
        private string? _path;

        private List<FileInfo> _filePaks = new();
        private List<FileInfo> _streamPaks = new();

        private List<AssetPtr> _file = new();
        private List<AssetPtr> _stream = new();
        private TextWriter? _logger;
        private string? _outputDir;
        private int _logLevel;
        private int _logFrequency;

        public IGMakerProject(string? path) : this(path, null) { }
        public IGMakerProject(string? path, TextWriter? logger)
        {
            _logLevel = 5;
            _logFrequency = 0;
            _logger = logger;
            RootPath = path;
        }

        public IGResult FindPaks(IGFlags flags, bool clear = true)
        {
            if (!Directory.Exists(_path))
            {
                _lastEx = new IOException($"Directory '{_path}' doesn't exist or is unavailable!");
                return IGResult.IOError;
            }
            if (clear) { ClearPaks(flags); }

            DirectoryInfo dirInf = new DirectoryInfo(_path);

            FileInfo[]? files;
            if (flags.HasFlag(IGFlags.Stream))
            {
                files = dirInf.GetFiles($"*{STREAM_EXT}", SearchOption.TopDirectoryOnly);
                foreach (var item in files)
                {
                    AddIfValid(item, IGFlags.Stream);
                }
            }

            if (flags.HasFlag(IGFlags.File))
            {
                files = dirInf.GetFiles($"*{FILE_EXT}", SearchOption.TopDirectoryOnly);
                foreach (var item in files)
                {
                    AddIfValid(item, IGFlags.File);
                }
            }
            return IGResult.Success;
        }

        private const int STACK_BUFF_SIZE = 49152;
        private const int HEAP_BUFF_SIZE = 67_108_864;

        public IGResult ReadAssets(IGFlags flags, bool clear = true)
        {
            if (clear) { ClearAssets(flags); }
            {
                Span<byte> lenBuf = stackalloc byte[4];
                try
                {
                    if (flags.HasFlag(IGFlags.Stream))
                    {
                        if (CanLog(0))
                        {
                            LogLine($"[{nameof(IGFlags.Stream)} - Asset Pointers]", LogType.Info);
                        }

                        int add = 0;
                        long pos = 0;
                        for (int i = 0; i < _streamPaks.Count; i++)
                        {
                            var pak = _streamPaks[i];
                            using (var fs = pak.OpenRead())
                            {
                                long totLen = fs.Length;
                                fs.Seek(64, SeekOrigin.Begin);
                                pos = 64;
                                while (true)
                                {
                                    var l = fs.Read(lenBuf);
                                    if (l < 4)
                                    {
                                        if (CanLog(0))
                                        {
                                            LogLine($"Reached end of IO Stream @{Extensions.GetSizeString(pos)} (Read {l} bytes)", LogType.Info);
                                        }
                                        break;
                                    }

                                    pos += 4;
                                    int len = MemoryMarshal.Read<int>(lenBuf);

                                    if (len < 4 || (totLen - pos) < len)
                                    {
                                        if (CanLog(0))
                                        {
                                            LogLine($"Reached end of PAK @0x{pos:X8} (Read {Extensions.GetSizeString(len)} as length and EOF is in {Extensions.GetSizeString((totLen - pos))})", LogType.Info);
                                        }
                                        break;
                                    }
                                    var ptr = new AssetPtr(i, GetBufferType(len), AssetType.Streamed, pos, len);
                                    if (CanLog(2))
                                    {
                                        LogLine($"Added [{nameof(IGFlags.Stream)}] asset pointer (@0x{ptr.pos:X8}, {Extensions.GetSizeString(ptr.size),-12}, PAK #{ptr.pakIdex}, Buffer: {ptr.bufferType})", LogType.Info);
                                    }

                                    _stream.Add(ptr);
                                    add++;

                                    pos += len;

                                    long mod = pos % 16;
                                    if (mod > 0)
                                    {
                                        long diff = 16 - mod;
                                        pos += diff;
                                        fs.Seek(diff + len, SeekOrigin.Current);
                                        continue;
                                    }
                                    fs.Seek(len, SeekOrigin.Current);
                                }
                            }
                        }

                        if (CanLog(0))
                        {
                            LogLine($"Added '{add}' stream entries!", LogType.Info);
                        }
                    }

                    if (flags.HasFlag(IGFlags.File))
                    {
                        if (CanLog(0))
                        {
                            LogLine($"[{nameof(IGFlags.File)} - Asset Pointers]", LogType.Info);
                        }

                        int add = _file.Count;
                        for (int i = 0; i < _filePaks.Count; i++)
                        {
                            var pak = _filePaks[i];
                            using (var fs = pak.OpenRead())
                            {
                                AddFiles(i, fs);
                            }
                        }

                        if (CanLog(0))
                        {
                            add = _file.Count - add;
                            LogLine($"Added '{add}' file entries!", LogType.Info);
                        }
                    }
                }
                catch (IOException e)
                {
                    _lastEx = e;
                    return IGResult.IOError;
                }
            }
            return IGResult.Success;
        }

        public IGResult ExtractAssets(IGFlags flags)
        {
            unsafe
            {
                IntPtr heap = Marshal.AllocHGlobal(HEAP_BUFF_SIZE);
                Span<byte> bufferStack = stackalloc byte[STACK_BUFF_SIZE];
                Span<byte> bufferHeap;
                bufferHeap = new Span<byte>(heap.ToPointer(), HEAP_BUFF_SIZE);

                string outPath = string.IsNullOrWhiteSpace(_outputDir) ? $"{_path}/Output" : _outputDir;
                IGResult res;
                if (flags.HasFlag(IGFlags.Stream))
                {
                    if (CanLog(0))
                    {
                        LogLine($"[{nameof(IGFlags.Stream)} - Asset Extraction]", LogType.Info);
                    }

                    string pth = $"{outPath}/Stream";
                    CreateDir(pth);

                    res = Extract("Stream", false, pth, _streamPaks, _stream, bufferStack, bufferHeap);
                    if(res != IGResult.Success)
                    {
                        Marshal.FreeHGlobal(heap);
                        return res;
                    }
                }

                if (flags.HasFlag(IGFlags.File))
                {
                    if (CanLog(0))
                    {
                        LogLine($"[{nameof(IGFlags.File)} - Asset Extraction]", LogType.Info);
                    }

                    string pth = $"{outPath}/File";
                    CreateDir(pth);

                    res = Extract("File", flags.HasFlag(IGFlags.GroupByType), pth, _filePaks, _file, bufferStack, bufferHeap);
                    if (res != IGResult.Success)
                    {
                        Marshal.FreeHGlobal(heap);
                        return res;
                    }
                }
                Marshal.FreeHGlobal(heap);
            }
            return IGResult.Success;
        }

        public void ClearPaks(IGFlags flags)
        {
            if (flags.HasFlag(IGFlags.Stream))
            {
                _streamPaks.Clear();
            }

            if (flags.HasFlag(IGFlags.File))
            {
                _filePaks.Clear();
            }
        }

        public void ClearAssets(IGFlags flags)
        {
            if (flags.HasFlag(IGFlags.Stream))
            {
                _stream.Clear();
            }

            if (flags.HasFlag(IGFlags.File))
            {
                _file.Clear();
            }
        }

        private IGResult Extract(string prefix, bool group, string pth, IList<FileInfo> paks, IList<AssetPtr> ptrs, Span<byte> bufferStack, Span<byte> bufferHeap)
        {
            if (group)
            {
                for (int i = 1; i < 3; i++)
                {
                    CreateDir($"{pth}/{(AssetType)i}");
                }
            }

            int ind = -1;
            int ext = 0;
            Stream? stream = null;
            for (int i = 0; i < ptrs.Count; i++)
            {
                var ptr = ptrs[i];

                if (ptr.pakIdex != ind)
                {
                    ind = ptr.pakIdex;
                    if (stream != null)
                    {
                        stream.Dispose();
                        stream = null;
                    }
                    stream = paks[ind].OpenRead();
                }

                if (stream == null)
                {
                    _lastEx = new IOException("Something went wrong in IO!");
                    if (CanLog(0))
                    {
                        LogLine($"Stream is NULL!", LogType.Error);
                    }
                    return IGResult.IOError;
                }

                Span<byte> buff = ptr.bufferType == BufferType.Stack ? bufferStack.Slice(0, ptr.size) : default;
                switch (ptr.bufferType)
                {
                    case BufferType.Heap:
                        buff = bufferHeap.Slice(0, ptr.size);
                        break;
                    case BufferType.Dynamic:
                        buff = new byte[ptr.size];
                        break;
                }

                stream.Seek(ptr.pos, SeekOrigin.Begin);
                int len = stream.Read(buff);
                if (len < 1) { continue; }

                string name = $"{prefix} #{i}{GetExtension(buff.Slice(0, len))}";
                using (var fs = new FileStream($"{pth}/{(group ? ptr.assetType.ToString() : "")}/{name}", FileMode.Create))
                {
                    fs.Write(buff);
                }

                ext++;
                if (CanLog(2) && (_logFrequency <= 0 || ((i % _logFrequency) == 0)))
                {
                    LogLine($"Extracted '{name}'", LogType.Info);
                }
            }

            ind = -1;
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            if (CanLog(0))
            {
                LogLine($"Extracted '{ext}' assets!", LogType.Info);
            }
            return IGResult.Success;
        }

        private void AddFiles(int pak, Stream strm)
        {
            const int BUFFER = 16 << 12;
            Span<byte> buffer = stackalloc byte[BUFFER];

            long pos = 0;
            long len = strm.Length;

            Span<long> headers = stackalloc long[HEADERS.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                headers[i] = -1;
            }

            int found = 0;

            while (true)
            {
                int read = strm.Read(buffer);
                int offset = read % 16;
                read -= offset;

                bool done = false;
                for (int i = 0; i < read; i+=16)
                {
                    FindHeader(pos + i, buffer.Slice(i, 16), headers, ref found);
                    if(found >= headers.Length)
                    {
                        done = true;
                        break;
                    }
                }

                if (done) { break; }
                pos += read;
                if(offset != 0 || (len - pos) < 16) { break; }
            }

            Span<byte> lenBuf = stackalloc byte[4];
            for (int i = 0; i < headers.Length; i++)
            {
                long p = headers[i];
                if (p > -1)
                {
                    p += 16;
                    strm.Seek(p, SeekOrigin.Begin);

                    strm.Read(lenBuf);
                    int count = MemoryMarshal.Read<int>(lenBuf);
                    int move = -1;
                    AssetType type = AssetType.Unknown;
                    int skip = 0;
                    switch (i)
                    {
                        case 0: //WAVE Data
                            strm.Seek(16, SeekOrigin.Current);
                            type = AssetType.Audio;
                            break;
                        case 1: //WALL Data
                            strm.Seek(8, SeekOrigin.Current);
                            type = AssetType.Texture;
                            skip = 8;
                            break;
                    }

                    strm.Read(lenBuf);
                    move = MemoryMarshal.Read<int>(lenBuf);

                    if (move < 0) { continue; }

                    strm.Seek(move, SeekOrigin.Begin);

                    if (CanLog(0))
                    {
                        LogLine($"Adding '{count}' [{type}] assets...", LogType.Info);
                    }

                    pos = move;
                    for (int j = 0; j < count; j++)
                    {
                        strm.Read(lenBuf);
                        if(skip > 0)
                        {
                            strm.Seek(skip, SeekOrigin.Current);
                        }
                        pos += 4 + skip;
                        int l = MemoryMarshal.Read<int>(lenBuf);

                        var ptr = new AssetPtr(pak, GetBufferType(l), type, pos, l);
                        if (CanLog(2) && (_logFrequency <= 0 || ((j % _logFrequency) == 0)))
                        {
                            LogLine($"Added [{nameof(IGFlags.File)}] asset pointer (@0x{ptr.pos:X8}, {Extensions.GetSizeString(ptr.size),-12}, PAK #{ptr.pakIdex}, Buffer: {ptr.bufferType})", LogType.Info);
                        }
                        
                        _file.Add(ptr);
                        pos += l;

                        long mod = pos % 16;
                        if (mod > 0)
                        {
                            long diff = 16 - mod;
                            pos += diff;
                            strm.Seek(diff + l, SeekOrigin.Current);
                            continue;
                        }
                        strm.Seek(l, SeekOrigin.Current);
                    }
                }
            }
        }

        private int GetBufferType(int len)
        {
            if (len > HEAP_BUFF_SIZE) { return 2; }
            if (len > STACK_BUFF_SIZE) { return 1; }
            return 0;
        }

        private string GetExtension(Span<byte> buffer)
        {
            uint header = MemoryMarshal.Read<uint>(buffer.Slice(0, 4));
            switch (header)
            {
                case 0x47_4E_50_89: return ".png";
                case 0x46_46_49_52:  //WAV
                    header = MemoryMarshal.Read<uint>(buffer.Slice(8, 4));
                    return header == 0x45_56_41_57 ? ".wav" : ".riff";
                default:
                    long totalCtrl = 0;
                    long totalChar = 0;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        char c = (char)buffer[i];
                        if ((c != '\n' && c != '\r') && char.IsControl(c))
                        {
                            totalCtrl++;
                            continue;
                        }
                        totalChar++;
                    }
                    float ratio = totalChar / (float)(totalCtrl < 1 ? 1.0f : totalCtrl);
                    return ratio > 1.0f ? ".txt" : ".bin";
            }
        }

        private void Log(ReadOnlySpan<char> message, LogType type)
        {
            if (_logger == null) { return; }
            WriteLogHeader(type);
            _logger.Write(message);
        }

        private void LogLine(ReadOnlySpan<char> message, LogType type)
        {
            if (_logger == null) { return; }
            WriteLogHeader(type);
            _logger.WriteLine(message);
        }

        private void WriteLogHeader(LogType type)
        {
            if (_logger == null) { return; }

            _logger.Write("[IGExtractor] - ");
            switch (type)
            {
                case LogType.Info:
                    _logger.Write(nameof(LogType.Info));
                    goto case (LogType)0xFF;
                case LogType.Warning:
                    _logger.Write(nameof(LogType.Warning));
                    goto case (LogType)0xFF;
                case LogType.Error:
                    _logger.Write(nameof(LogType.Error));
                    goto case (LogType)0xFF;
                case (LogType)0xFF:
                    _logger.Write(": ");
                    break;
            }
        }

        private void AddIfValid(FileInfo info, IGFlags type)
        {
            Span<byte> curBuf = stackalloc byte[16];

            IList<FileInfo>? paks = null;
            switch (type)
            {
                case IGFlags.Stream:
                    paks = _streamPaks;
                    Encoding.ASCII.GetBytes(STREAM_HEADER, curBuf);
                    break;
                case IGFlags.File:
                    paks = _filePaks;
                    Encoding.ASCII.GetBytes(FILES_HEADER, curBuf);
                    break;
            }

            if (paks != null)
            {
                if (ContainsPak(paks, info))
                {
                    if (CanLog(0))
                    {
                        LogLine($"Already contains [{type}] pak '{info.Name}'", LogType.Warning);
                    }
                    return;
                }
                Span<byte> buffer = stackalloc byte[16];

                using (var fs = info.OpenRead())
                {
                    var read = fs.Read(buffer);
                    if (read < 16 || !buffer.SequenceEqual(curBuf))
                    {
                        if (CanLog(0))
                        {
                            LogLine($"'{info.Name}' is not a pak of type '{type}'!", LogType.Warning);
                        }
                        return;
                    }
                }
                paks.Add(info);
                if (CanLog(0))
                {
                    LogLine($"Added '{info.Name}' to '{type}' pak list.", LogType.Info);
                }
            }
        }

        private void FindHeader(long pos, Span<byte> data, Span<long> headers, ref int found)
        {
            for (int i = 0; i < HEADERS.Length; i++)
            {
                var hdr = HEADERS[i];
                if (headers[i] < 0 && hdr.Span.SequenceEqual(data)) 
                {
                    headers[i] = pos;
                    found++;
                    break;
                }
            }
        }

        private void CreateDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                if (CanLog(1))
                {
                    LogLine($"Created new path @'{path}'", LogType.Info);
                }
            }
        }

        private bool CanLog(int level) => _logLevel > level && _logger != null;

        private bool ContainsPak(IList<FileInfo> paks, FileInfo inf)
        {
            for (int i = 0; i < paks.Count; i++)
            {
                if (DUPE_PRED(paks[i], inf)) { return true; }
            }
            return false;
        }
    }
}