using System;

namespace Yutbube.Models
{
    public  class ConversionInfo
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string TempPath { get; set; }
        public DateTime Expiration { get; set; }
    }
}