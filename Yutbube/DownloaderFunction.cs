using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tyrrrz.Extensions;
using YoutubeExplode;
using YoutubeExplode.Models;
using YoutubeExplode.Models.MediaStreams;

namespace Yutbube
{
    public static class DownloaderFunction
    {
        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();
        private static readonly Cli FfmpegCli = new Cli(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("file:\\", string.Empty), "ffmpeg.exe"));

        private static readonly string TempDirectoryPath = Path.GetTempPath();
        private static readonly string OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "Output");

        [FunctionName("Downloader")]
        public static async Task Run(
            [QueueTrigger("conversion", Connection = "AzureWebJobsStorage")]QueueMessagePayload payload,
            [SignalR(HubName = "broadcast")]IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation("Downloader triggered");
            var video = payload.Video;
            ConversionInfo ci = null;
            try
            {
                ci = await DownloadAndConvertVideo(video, log);
                log.LogInformation("Uploading to Blob Storage...");
                video.StorageUrl = await BlobStorageRepository.Upload(video.Id, ci.TempPath);
                log.LogInformation($"Signaling the readiness of {ci.Id}");
                await signalRMessages.AddAsync(new SignalRMessage
                {
                    Target = payload.ClientId,
                    Arguments = new object[]
                    {
                        video
                    }
                });
            }
            catch (Exception ex)
            {
                log.Log(LogLevel.Error, ex.Message, ex);
                video.Error = ex.Message;
                await signalRMessages.AddAsync(new SignalRMessage
                {
                    Target = payload.ClientId,
                    Arguments = new object[]
                    {
                        video
                    }
                });
            }
            finally
            {
                if (ci != null && File.Exists(ci.TempPath))
                {
                    log.LogInformation("Deleting audio temp file...");
                    File.Delete(ci.TempPath);
                }
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

        private static async Task<ConversionInfo> DownloadAndConvertVideo(StorageItem video, ILogger log)
        {
            log.LogInformation($"Working on video [{video.Id}]...");

            var set = await YoutubeClient.GetVideoMediaStreamInfosAsync(video.Id);
            var cleanTitle = $"{video.Title.Replace(Path.GetInvalidFileNameChars(), '_')}.mp3";
            log.LogInformation($"{video.Title}");

            // Get highest bitrate audio-only or highest quality mixed stream
            var streamInfo = GetBestAudioStreamInfo(set);

            // Download to temp file
            log.LogInformation("Downloading...");
            var streamFileExt = streamInfo.Container.GetFileExtension();
            var streamFilePath = Path.Combine(TempDirectoryPath, $"{Guid.NewGuid()}.{streamFileExt}");
            using (var streamFile = new FileStream(streamFilePath, FileMode.Create))
            {
                await YoutubeClient.DownloadMediaStreamAsync(streamInfo, streamFile);
            }

            // Convert to mp3
            log.LogInformation("Converting...");
            Directory.CreateDirectory(OutputDirectoryPath);
            var outputFilePath = Path.Combine(OutputDirectoryPath, cleanTitle);
            await FfmpegCli.ExecuteAsync($"-i \"{streamFilePath}\" -q:a 0 -map a \"{outputFilePath}\" -y");

            // Delete temp file
            log.LogInformation("Deleting video temp file...");
            File.Delete(streamFilePath);

            // Edit mp3 metadata
            log.LogInformation("Writing metadata...");
            var idMatch = Regex.Match(video.Title, @"^(?<artist>.*?)-(?<title>.*?)$");
            var artist = idMatch.Groups["artist"].Value.Trim();
            var title = idMatch.Groups["title"].Value.Trim();
            using (var meta = TagLib.File.Create(outputFilePath))
            {
                meta.Tag.Performers = new[] { artist };
                meta.Tag.Title = title;
                meta.Save();
            }

            log.LogInformation("Conversion complete.");
            return new ConversionInfo
            {
                Id = video.Id,
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