using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YoutubeExplode.Models;

namespace Yutbube.Models
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
        public string FileName => !string.IsNullOrEmpty(StorageUrl) ? Path.GetFileName(StorageUrl) : null;

        [JsonProperty("isReady")]
        public bool IsReady => !string.IsNullOrEmpty(StorageUrl) || !string.IsNullOrEmpty(Error);

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("conversionDate")]
        public string ConversionDate { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> Properties =>
            new Dictionary<string, string>
            {
                {"Video ID", Id},
                {"Video title", Title},
                {"Storage URL", StorageUrl},
                {"Duration", Duration.ToString()},
                {"Error", Error}
            };

        public StorageItem() { }

        public StorageItem(Video video, string storageUrl)
        {
            Id = video.Id;
            Title = video.Title;
            Duration = video.Duration;
            Thumbnail = video.Thumbnails.LowResUrl;
            StorageUrl = storageUrl;
        }
    }
}