using System.Text.RegularExpressions;

namespace Yutbube
{
    public static class FfmpegStatus
    {
        private const string NumberFormat = @"[\d\.]+";
        private static readonly string SizePart = $@"\s*(?<size>{NumberFormat})kB";
        private const string TimePart = @"\s*(?<time>[\d\.:]+)";
        private static readonly string BitratePart = $@"\s*(?<bitrate>{NumberFormat})kbits\/s";
        private static readonly string SpeedPart = $@"\s*(?<speed>{NumberFormat})x";

        private static readonly string MatchLine = $@"size={SizePart}\s+time={TimePart}\s+bitrate={BitratePart}\s+speed={SpeedPart}";

        public static Match Match(string s)
        {
            return new Regex(MatchLine).Match(s);
        }
    }
}