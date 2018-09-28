using System.IO;
using Newtonsoft.Json;

namespace YouTubeDownloader
{
    public class ResponseData
    {
        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("url")]
        public string StorageUrl { get; }

        [JsonProperty("fileName")]
        public string FileName { get; }

        [JsonProperty("isReady")]
        public bool IsReady => !string.IsNullOrEmpty(StorageUrl) || !string.IsNullOrEmpty(Error);

        [JsonProperty("error")]
        public string Error { get; set; }

        public ResponseData(string id, string storageUrl)
        {
            Id = id;
            StorageUrl = storageUrl;
            FileName = Path.GetFileName(storageUrl);
        }
    }
}