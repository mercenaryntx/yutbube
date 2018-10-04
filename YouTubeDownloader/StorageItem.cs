using System;
using System.IO;
using Newtonsoft.Json;
using YoutubeExplode.Models;

namespace YouTubeDownloader
{
    public class StorageItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("duration")]
        public TimeSpan Duration { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonProperty("url")]
        public string StorageUrl { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("isReady")]
        public bool IsReady => !string.IsNullOrEmpty(StorageUrl) || !string.IsNullOrEmpty(Error);

        [JsonProperty("error")]
        public string Error { get; set; }

        public StorageItem()
        {
        }

        public StorageItem(Video video, string storageUrl)
        {
            Id = video.Id;
            Title = video.Title;
            Duration = video.Duration;
            Thumbnail = video.Thumbnails.LowResUrl;
            StorageUrl = storageUrl;
            FileName = Path.GetFileName(storageUrl);
        }
    }
}