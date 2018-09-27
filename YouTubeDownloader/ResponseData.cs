using System.IO;

namespace YouTubeDownloader
{
    public class ResponseData
    {
        public string Id { get; }
        public string StorageUrl { get; }
        public bool IsReady { get; }

        public ResponseData(string id, string storageUrl)
        {
            Id = id;
            StorageUrl = storageUrl;
            IsReady = !string.IsNullOrEmpty(storageUrl);
        }
    }

    //public class ResponseData
    //{
    //    public MemoryStream Content { get; }
    //    public string FileName { get; }

    //    public ResponseData(MemoryStream content, string fileName)
    //    {
    //        Content = content;
    //        FileName = fileName;
    //    }
    //}
}