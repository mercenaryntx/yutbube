using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace YouTubeDownloader
{
    public static class BlobStorageRepository
    {
        private static CloudBlobClient _client;
        private static CloudBlobClient Client
        {
            get
            {
                if (_client == null)
                {
                    CloudStorageAccount.TryParse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), out var storageAccount);
                    _client = storageAccount.CreateCloudBlobClient();
                }
                return _client;
            }
        }

        private static CloudBlobContainer Container => Client.GetContainerReference(Environment.GetEnvironmentVariable("AzureStorageCacheContainer"));

        public static async Task<string> GetTempFileStorageUrl(string key)
        {
            var folder = Container.GetDirectoryReference(key);
            var page = await folder.ListBlobsSegmentedAsync(null);
            var blob = page.Results.FirstOrDefault();
            return blob?.StorageUri.PrimaryUri.ToString();
        }

        public static async Task<string> Upload(string key, string tempFilePath)
        {
            var folder = Container.GetDirectoryReference(key);
            var blob = folder.GetBlockBlobReference(Path.GetFileName(tempFilePath));
            await blob.UploadFromFileAsync(tempFilePath);
            return blob.StorageUri.PrimaryUri.ToString();
        }
    }
}