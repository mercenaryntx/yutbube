using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Tyrrrz.Extensions;
using YoutubeExplode;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Yutbube
{
    public static class EnqueuerFunction
    {
        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();
        private static readonly string QueueName = Environment.GetEnvironmentVariable("AzureStorageConversionQueueName");
        private static readonly CloudQueueClient QueueClient;

        static EnqueuerFunction()
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            QueueClient = storageAccount.CreateCloudQueueClient();
        }

        [FunctionName("Enqueuer")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("Enqueuer triggered");

            var clientId = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "c").Value;
            if (string.IsNullOrEmpty(clientId))
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Client Id is missing");
            }

            try
            {
                var result = new ConcurrentBag<StorageItem>();
                var ids = await ParseRequest(req);
                var tasks = ids.Select(async id =>
                {
                    var storageItem = await GetVideo(id, log);
                    var url = await GetTempFileStorageUrl(id, log);

                    storageItem.StorageUrl = url;
                    if (storageItem.StorageUrl == null && storageItem.Error == null)
                    {
                        var content = new QueueMessagePayload(storageItem, clientId);
                        var message = new CloudQueueMessage(JsonConvert.SerializeObject(content));
                        log.LogInformation($"[{id}] Enqueuing message");
                        await QueueClient.GetQueueReference(QueueName).AddMessageAsync(message);
                        storageItem.Message = "Enqueued";
                    }
                    result.Add(storageItem);
                }).ToArray();

                Task.WaitAll(tasks);

                return req.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                log.Log(LogLevel.Error, ex.Message, ex);
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        private static async Task<StorageItem> GetVideo(string id, ILogger log)
        {
            try
            {
                log.LogInformation($"[{id}] Getting video info");
                return new StorageItem(await YoutubeClient.GetVideoAsync(id), null);
            }
            catch (Exception ex)
            {
                return new StorageItem
                {
                    Id = id,
                    Error = ex.Message
                };
            }
        }

        private static Task<string> GetTempFileStorageUrl(string id, ILogger log)
        {
            log.LogInformation($"[{id}] Checking blob storage");
            return BlobStorageRepository.GetTempFileStorageUrl(id);
        }

        private static async Task<IEnumerable<string>> ParseRequest(HttpRequestMessage req)
        {
            var param = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "v");
            var v = param.Value;
            if (string.IsNullOrEmpty(v)) v = await req.Content.ReadAsStringAsync();
            var result = new HashSet<string>();
            foreach (var p in v.Split(Environment.NewLine, ",", " "))
            {
                if (!YoutubeClient.ValidatePlaylistId(p))
                {
                    result.Add(p);
                    continue;
                }

                var playlist = await YoutubeClient.GetPlaylistAsync(p);
                playlist.Videos.ForEach(video => result.Add(video.Id));
            }
            return result;
        }
    }
}