using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace YouTubeDownloader
{
    public static class DownloaderFunction
    {
        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();
        private static readonly Cli FfmpegCli = new Cli(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("file:\\", string.Empty), "ffmpeg.exe"));

        private static readonly string TempDirectoryPath = Path.GetTempPath();
        private static readonly string OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "Output");

        [FunctionName("Downloader")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req,
            [SignalR(HubName = "broadcast")]IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.Log(LogLevel.Information, "C# HTTP trigger function processed a request.");

            try
            {
                var result = new List<ResponseData>();
                var ids = await ParseRequest(req);
                foreach (var id in ids)
                {
                    var storageUrl = await BlobStorageRepository.GetTempFileStorageUrl(id);
                    if (storageUrl == null)
                    {
                        var worker = new Task(async () =>
                        {
                            var idid = id;
                            try
                            {
                                var ci = await DownloadAndConvertVideo(idid, log);
                                var url = await BlobStorageRepository.Upload(id, ci.TempPath);
                                log.Log(LogLevel.Information, $"Signaling the readiness of {ci.Id}");
                                await signalRMessages.AddAsync(new SignalRMessage
                                {
                                    Target = "notify",
                                    Arguments = new object[]
                                    {
                                        new ResponseData(idid, url)
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                log.Log(LogLevel.Error, ex.Message, ex);
                                await signalRMessages.AddAsync(new SignalRMessage
                                {
                                    Target = "notify",
                                    Arguments = new object[]
                                    {
                                        new ResponseData(idid, null)
                                        {
                                            Error = ex.Message
                                        }
                                    }
                                });
                            }
                        }, TaskCreationOptions.LongRunning);
                        worker.Start();
                    }
                    result.Add(new ResponseData(id, storageUrl));
                }

                return req.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                log.Log(LogLevel.Error, ex.Message, ex);
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        private static async Task<IEnumerable<string>> ParseRequest(HttpRequestMessage req)
        {
            var param = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "v");
            var v = param.Value;
            if (string.IsNullOrEmpty(v)) v = await req.Content.ReadAsStringAsync();
            var result = new List<string>();
            foreach (var p in v.Split(Environment.NewLine, ",", " "))
            {
                if (!YoutubeClient.ValidatePlaylistId(p))
                {
                    result.Add(p);
                    continue;
                }

                var playlist = await YoutubeClient.GetPlaylistAsync(p);
                result.AddRange(playlist.Videos.Select(video => video.Id));
            }
            return result;
        }

        private static async Task<ConversionInfo> DownloadAndConvertVideo(string id, ILogger log)
        {
            log.Log(LogLevel.Information, $"Working on video [{id}]...");

            // Get video info
            var video = await YoutubeClient.GetVideoAsync(id);
            var set = await YoutubeClient.GetVideoMediaStreamInfosAsync(id);
            var cleanTitle = $"{video.Title.Replace(Path.GetInvalidFileNameChars(), '_')}.mp3";
            log.Log(LogLevel.Information, $"{video.Title}");

            // Get highest bitrate audio-only or highest quality mixed stream
            var streamInfo = GetBestAudioStreamInfo(set);

            // Download to temp file
            log.Log(LogLevel.Information, "Downloading...");
            var streamFileExt = streamInfo.Container.GetFileExtension();
            var streamFilePath = Path.Combine(TempDirectoryPath, $"{Guid.NewGuid()}.{streamFileExt}");
            using (var streamFile = new FileStream(streamFilePath, FileMode.Create))
            {
                await YoutubeClient.DownloadMediaStreamAsync(streamInfo, streamFile);
            }

            // Convert to mp3
            log.Log(LogLevel.Information, $"Converting... (Ffmpeg path: {FfmpegCli.FilePath})");
            Directory.CreateDirectory(OutputDirectoryPath);
            var outputFilePath = Path.Combine(OutputDirectoryPath, cleanTitle);
            await FfmpegCli.ExecuteAsync($"-i \"{streamFilePath}\" -q:a 0 -map a \"{outputFilePath}\" -y");

            // Delete temp file
            log.Log(LogLevel.Information, "Deleting temp file #1...");
            File.Delete(streamFilePath);

            // Edit mp3 metadata
            log.Log(LogLevel.Information, "Writing metadata...");
            var idMatch = Regex.Match(video.Title, @"^(?<artist>.*?)-(?<title>.*?)$");
            var artist = idMatch.Groups["artist"].Value.Trim();
            var title = idMatch.Groups["title"].Value.Trim();
            using (var meta = TagLib.File.Create(outputFilePath))
            {
                meta.Tag.Performers = new[] { artist };
                meta.Tag.Title = title;
                meta.Save();
            }

            log.Log(LogLevel.Information, "Conversion complete.");
            return new ConversionInfo
            {
                Id = id,
                FileName = cleanTitle,
                TempPath = outputFilePath,
                Expiration = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30))
            };
        }

        private static MediaStreamInfo GetBestAudioStreamInfo(MediaStreamInfoSet set)
        {
            if (set.Audio.Any()) return set.Audio.WithHighestBitrate();
            if (set.Muxed.Any()) return set.Muxed.WithHighestVideoQuality();
            throw new Exception("No applicable media streams found for this video");
        }
    }
}