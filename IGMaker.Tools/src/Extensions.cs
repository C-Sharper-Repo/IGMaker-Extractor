using System.Globalization;

namespace IGMaker.Tools
{
    internal static class Extensions
    {
        private static readonly string[] SIZE_SUFFIXES = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string GetSizeString(this long size, int decimals = 1)
        {
            decimals = decimals < 0 ? 0 : decimals;
            if(size < 0) { return GetSizeString(-size, decimals); }

            if(size == 0) { return string.Format(CultureInfo.InvariantCulture, $"{{0:n{decimals}}} bytes", 0); }

            int mag = (int)Math.Log(size, 1024);
            decimal adjustedSize = (decimal)size / (1L << (mag * 10));

            if(Math.Round(adjustedSize, decimals) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }
            return string.Format(CultureInfo.InvariantCulture, $"{{0:n{decimals}}} {{1}}", adjustedSize, SIZE_SUFFIXES[mag]);
        }
    }
}