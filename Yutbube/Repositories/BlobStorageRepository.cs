using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Yutbube.Models;

namespace Yutbube.Repositories
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

        public static async Task<string> Upload(string key, string tempFilePath, CancellationToken cancellationToken)
        {
            var folder = Container.GetDirectoryReference(key);
            var blob = folder.GetBlockBlobReference(Path.GetFileName(tempFilePath));
            await blob.UploadFromFileAsync(tempFilePath, null, null, null, cancellationToken);
            return blob.StorageUri.PrimaryUri.ToString();
        }

        public static async Task<string> Upload<T>(string key, T obj, CancellationToken cancellationToken)
        {
            var folder = Container.GetDirectoryReference(key);
            var blob = folder.GetBlockBlobReference("index.json");
            await blob.UploadTextAsync(JsonConvert.SerializeObject(obj), null, null, null, null, null, cancellationToken);
            return blob.StorageUri.PrimaryUri.ToString();
        }

        public static async Task<StorageItem[]> List()
        {
            BlobContinuationToken continuationToken = null;
            var results = new ConcurrentBag<StorageItem>();
            do
            {
                var response = await Container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                var tasks = response.Results.Select(async item =>
                {
                    if (item is CloudBlobDirectory folder)
                    {
                        var blob = folder.GetBlockBlobReference("index.json");
                        if (await blob.ExistsAsync())
                        {
                            var json = await blob.DownloadTextAsync();
                            results.Add(JsonConvert.DeserializeObject<StorageItem>(json));
                        }
                    }
                }).ToArray();

                Task.WaitAll(tasks);
            }
            while (continuationToken != null);
            return results.ToArray();
        }
    }
}