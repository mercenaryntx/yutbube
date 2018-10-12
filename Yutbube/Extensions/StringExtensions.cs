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
    }
}