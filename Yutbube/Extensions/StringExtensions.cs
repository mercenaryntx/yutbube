using System.Text.RegularExpressions;

namespace Yutbube.Extensions
{
    public static class StringExtensions
    {
        public static string TrimSpaces(this string input)
        {
            var r = new Regex(@"(\s+?)\1+");
            return r.Replace(input.Trim(), " ");
        }

        public static string EscapeCurlyBraces(this string input)
        {
            var r = new Regex(@"[\{\}]");
            return r.Replace(input, "$0$0");
        }
    }
}