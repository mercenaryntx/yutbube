using System.IO;
using System.Reflection;

namespace Yutbube.Conversion
{
    public static class ConversionConfiguration
    {
        public static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("file:\\", string.Empty);
        public static readonly string TempDirectoryPath = Path.GetTempPath();
        public static readonly string OutputDirectoryPath = Path.Combine(TempDirectoryPath, "Output");
    }
}