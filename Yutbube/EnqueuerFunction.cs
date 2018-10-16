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
using Yutbube.Extensions;
using Yutbube.Models;
using Yutbube.Repositories;

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

        [FunctionName("enqueuer")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("Enqueuer triggered");

            var clientId = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "c").Value;
            if (string.IsNullOrEmpty(clientId)) return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Client Id is missing");

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
                log.LogError(ex, ex.Message.EscapeCurlyBraces());
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        private static async Task<IEnumerable<string>> ParseRequest(HttpRequestMessage req)
        {
            var param = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "v");
            var v = param.Value;
            if (string.IsNullOrEmpty(v)) v = await req.Content.ReadAsStringAsync();
            var result = new HashSet<string>();
            foreach (var p in v.Split(Environment.NewLine, ",", " "))
            {
                var playlistId = GetPlaylistId(p);
                if (string.IsNullOrEmpty(playlistId))
                {
                    var videoId = GetVideoId(p);
                    if (!string.IsNullOrEmpty(videoId)) result.Add(videoId);
                }
                else
                {
                    var playlist = await YoutubeClient.GetPlaylistAsync(playlistId);
                    playlist.Videos.ForEach(video => result.Add(video.Id));
                }
            }
            return result;
        }

        private static string GetPlaylistId(string input)
        {
            if (YoutubeClient.ValidatePlaylistId(input)) return input;
            return YoutubeClient.TryParsePlaylistId(input, out var playlistId) ? playlistId : null;
        }

        private static string GetVideoId(string input)
        {
            if (YoutubeClient.ValidateVideoId(input)) return input;
            return YoutubeClient.TryParseVideoId(input, out var videoId) ? videoId : null;
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
    }
}