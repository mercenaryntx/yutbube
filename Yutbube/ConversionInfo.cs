using System;
using Newtonsoft.Json;

namespace Yutbube
{
    public  class ConversionInfo
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string TempPath { get; set; }
        public DateTime Expiration { get; set; }
    }
}