using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using Yutbube.Extensions;
using Yutbube.Models;
using Yutbube.Repositories;
using Microsoft.ApplicationInsights;

namespace Yutbube
{
    public static class DownloaderFunction
    {
        private const string CONVERTING = "Converting...";

        private static readonly TelemetryClient TelemetryClient;
        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();

        private static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("file:\\", string.Empty);
        private static readonly string TempDirectoryPath = Path.GetTempPath();
        private static readonly string OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "Output");

        static DownloaderFunction()
        {
            var key = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(key))
                TelemetryClient = new TelemetryClient
                {
                    InstrumentationKey = key
                };
        }

        [FunctionName("downloader")]
        public static async Task Run(
            [QueueTrigger("%AzureStorageConversionQueueName%", Connection = "AzureWebJobsStorage")]QueueMessagePayload payload,
            [SignalR(HubName = "broadcast")]IAsyncCollector<SignalRMessage> signalRMessages,
            ExecutionContext context,
            ILogger log)
        {
            var sw = new Stopwatch();
            sw.Start();
            log.LogInformation("Downloader triggered");

            if (TelemetryClient != null)
            {
                TelemetryClient.Context.Operation.Id = context.InvocationId.ToString();
                TelemetryClient.Context.Operation.Name = "downloader";
                TelemetryClient.Context.Session.Id = payload.ClientId;
            }

            var video = payload.Video;
            string videoTempPath = null;
            string audioTempPath = null;
            int? previousPercentage = null;
            long? cliDuration = null;

            void Publish(string s)
            {
                video.Message = s;
                signalRMessages.Publish(payload.ClientId, video);
            }

            void ProgressNotifier(string s)
            {
                var m = FfmpegStatus.Match(s);
                if (m.Success && TimeSpan.TryParse(m.Groups["time"].Value, CultureInfo.InvariantCulture, out var time))
                {
                    var p = (int)Math.Floor(time.TotalMilliseconds * 100 / video.Duration.TotalMilliseconds);
                    if (previousPercentage == p) return;
                    Publish($"{CONVERTING} ({p}%)");
                    previousPercentage = p;
                }
            }

            try
            {
                Publish("Processing...");
                var streamInfo = await ProcessVideo(video, log);

                Publish("Downloading...");
                videoTempPath = await DownloadVideo(streamInfo, log);

                Publish(CONVERTING);
                var result = await ConvertToAudio(video, videoTempPath, log, ProgressNotifier);
                cliDuration = result.Item1.RunTime.Ticks;
                audioTempPath = result.Item2;
                WriteId3Tag(video, audioTempPath, log);

                Publish("Storing...");
                await UploadAudio(video, audioTempPath, log);
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message.EscapeCurlyBraces());
                video.Error = ex.Message;
                TelemetryClient?.TrackException(ex, video.Properties);
            }
            finally
            {
                if (videoTempPath != null && File.Exists(videoTempPath)) File.Delete(videoTempPath);
                if (audioTempPath != null && File.Exists(audioTempPath)) File.Delete(audioTempPath);
                Publish(string.Empty);
                TelemetryClient?.TrackEvent("Download", video.Properties,
                    new Dictionary<string, double>
                    {
                        {"Video duration", video.Duration.Ticks},
                        {"Function duration", sw.Elapsed.Ticks},
                        {"Conversion duration", cliDuration ?? 0}
                    });
            }
        }

        private static async Task<MediaStreamInfo> ProcessVideo(StorageItem video, ILogger log)
        {
            log.LogInformation($"Working on video [{video.Id}]...");

            var set = await YoutubeClient.GetVideoMediaStreamInfosAsync(video.Id);
            log.LogInformation($"{video.Title}");

            // Get highest bitrate audio-only or highest quality mixed stream
            return set.GetBestAudioStreamInfo();
        }

        private static async Task<string> DownloadVideo(MediaStreamInfo streamInfo, ILogger log)
        {
            log.LogInformation("Downloading...");
            var streamFileExt = streamInfo.Container.GetFileExtension();
            var streamFilePath = Path.Combine(TempDirectoryPath, $"{Guid.NewGuid()}.{streamFileExt}");
            using (var streamFile = new FileStream(streamFilePath, FileMode.Create))
            {
                await YoutubeClient.DownloadMediaStreamAsync(streamInfo, streamFile);
            }
            return streamFilePath;
        }

        private static async Task<Tuple<ExecutionResult, string>> ConvertToAudio(StorageItem video, string tempPath, ILogger log, Action<string> progress)
        {
            log.LogInformation("Converting...");
            Directory.CreateDirectory(OutputDirectoryPath);
            var cleanTitle = CleanTitle(video.Title);
            var mp3Path = Path.Combine(OutputDirectoryPath, cleanTitle);

            var result = await new Cli(Path.Combine(WorkingDirectory, "ffmpeg.exe"))
                .SetArguments($"-i \"{tempPath}\" -q:a 0 -map a \"{mp3Path}\" -y")
                .EnableStandardErrorValidation(false)
                .SetStandardOutputCallback(progress)
                .SetStandardErrorCallback(progress)
                .ExecuteAsync();

            log.LogInformation($"Conversion duration: {result.RunTime} (video duration: {video.Duration})");
            return new Tuple<ExecutionResult, string>(result, mp3Path);
        }

        private static string CleanTitle(string title)
        {
            //TODO
            return $"{title.Replace(Path.GetInvalidFileNameChars(), '_')}.mp3";
        }

        private static void WriteId3Tag(StorageItem video, string tempPath, ILogger log)
        {
            log.LogInformation("Writing metadata...");
            var idMatch = Regex.Match(video.Title, @"^(?<artist>.*?)\s+-\s+(?<title>.*?)$");
            if (!idMatch.Success) return;

            var artist = idMatch.Groups["artist"].Value.TrimSpaces();
            var title = idMatch.Groups["title"].Value.TrimSpaces();

            using (var meta = TagLib.File.Create(tempPath))
            {
                meta.Tag.Performers = new[] {artist};
                meta.Tag.Title = title;
                meta.Save();
            }
        }

        private static async Task UploadAudio(StorageItem item, string mp3Location, ILogger log)
        {
            log.LogInformation("Uploading to Blob Storage...");
            item.Message = string.Empty;
            item.ConversionDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            item.StorageUrl = await BlobStorageRepository.Upload(item.Id, mp3Location);
            await BlobStorageRepository.Upload(item.Id, item);
        }
    }
}