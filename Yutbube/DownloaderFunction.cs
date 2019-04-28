using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
using Yutbube.Conversion;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;

namespace Yutbube
{
    public static class DownloaderFunction
    {
        private const string CONVERTING = "Converting...";

        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();
        private static readonly AppInsightsClient AppInsightsClient = new AppInsightsClient();

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

            AppInsightsClient.SetOperation(context.InvocationId.ToString(), "downloader").SetSessionId(payload.ClientId);

            var video = payload.Video;
            video.DownloaderInvocationId = context.InvocationId;
            var cts = new CancellationTokenSource();
            string videoTempPath = null;
            string audioTempPath = null;
            int? previousPercentage = null;
            long? cliDuration = null;

            bool Publish(string s)
            {
                if (Cancellation.Tokens.ContainsKey(video.DownloaderInvocationId))
                {
                    video.Error = "Download cancelled";
                    if (!cts.IsCancellationRequested) cts.Cancel();
                    return false;
                }
                video.Message = s;
                signalRMessages.Publish("inprogress", video); //payload.ClientId
                return true;
            }

            void ProgressNotifier(string s)
            {
                if (Cancellation.Tokens.ContainsKey(video.DownloaderInvocationId))
                {
                    cts.Cancel();
                    return;
                }

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
                MediaStreamInfo streamInfo = null;
                if (Publish("Processing..."))
                {
                    streamInfo = await ProcessVideo(video, log);
                }

                if (Publish("Downloading..."))
                {
                    videoTempPath = await DownloadVideo(streamInfo, video, log, cts.Token);
                }

                if (Publish(CONVERTING))
                {
                    var result = await ConvertToAudio(video, videoTempPath, log, ProgressNotifier, cts.Token);
                    cliDuration = result.Item1.RunTime.Ticks;
                    audioTempPath = result.Item2;
                    WriteId3Tag(video, audioTempPath, log);
                }

                if (Publish("Storing..."))
                {
                    await UploadAudio(video, audioTempPath, log, cts.Token);
                }
            }
            catch (TaskCanceledException ex)
            {
                log.LogInformation("Download cancelled");
                video.Error = ex.Message;
                AppInsightsClient.TrackException(ex, video.Properties);
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message.EscapeCurlyBraces());
                video.Error = ex.Message;
                AppInsightsClient.TrackException(ex, video.Properties);
            }
            finally
            {
                Cancellation.Tokens.TryRemove(video.DownloaderInvocationId, out var value);
                cts.Dispose();
                if (videoTempPath != null && File.Exists(videoTempPath)) File.Delete(videoTempPath);
                if (audioTempPath != null && File.Exists(audioTempPath)) File.Delete(audioTempPath);
                Publish(string.Empty);
                AppInsightsClient.TrackEvent("Download", video.Properties,
                    new Dictionary<string, double>
                    {
                        {"Video duration", video.Duration.Ticks},
                        {"Function duration", sw.Elapsed.Ticks},
                        {"Conversion duration", cliDuration ?? 0},
                        { "Cancelled", cts.IsCancellationRequested ? 1 : 0 }
                    });
            }
        }

        private static async Task<MediaStreamInfo> ProcessVideo(StorageItem video, ILogger log)
        {
            log.LogInformation($"Working on video [{video.Id}]...");

            var set = await YoutubeClient.GetVideoMediaStreamInfosAsync(video.Id);
            log.LogInformation($"{video.Title}");

            //return set.GetAll().OrderByDescending(v => v.Size).First(v => v.Container == Container.Mp4);
            return set.GetBestAudioStreamInfo();
        }

        private static async Task<string> DownloadVideo(MediaStreamInfo streamInfo, StorageItem video, ILogger log, CancellationToken cancellationToken)
        {
            log.LogInformation("Downloading...");
            var cleanTitle = CleanTitle(video.Title);
            var streamFileExt = streamInfo.Container.GetFileExtension();
            var streamFilePath = Path.Combine(ConversionConfiguration.TempDirectoryPath, cleanTitle.Replace(".mp3", "." + streamFileExt));

            using (var streamFile = new FileStream(streamFilePath, FileMode.Create))
            {
                await YoutubeClient.DownloadMediaStreamAsync(streamInfo, streamFile, null, cancellationToken);
            }
            return streamFilePath;
        }

        private static async Task<Tuple<ExecutionResult, string>> ConvertToAudio(StorageItem video, string tempPath, ILogger log, Action<string> progress, CancellationToken cancellationToken)
        {
            log.LogInformation("Converting...");
            Directory.CreateDirectory(ConversionConfiguration.OutputDirectoryPath);
            var cleanTitle = CleanTitle(video.Title);
            var mp3Path = Path.Combine(ConversionConfiguration.OutputDirectoryPath, cleanTitle);

            var result = await new Cli(Path.Combine(ConversionConfiguration.WorkingDirectory, "ffmpeg.exe"))
                .SetArguments($"-i \"{tempPath}\" -q:a 0 -map a \"{mp3Path}\" -y")
                .EnableStandardErrorValidation(false)
                .SetStandardOutputCallback(progress)
                .SetStandardErrorCallback(progress)
                .SetCancellationToken(cancellationToken)
                .ExecuteAsync();

            log.LogInformation($"Conversion duration: {result.RunTime} (video duration: {video.Duration})");
            return new Tuple<ExecutionResult, string>(result, mp3Path);
        }

        private static string CleanTitle(string title)
        {
            var chars = Regex.Escape(string.Join(string.Empty, Path.GetInvalidFileNameChars()));
            var r = new Regex($"[{chars}]");
            return $"{r.Replace(title, "_")}.mp3";
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

        private static async Task UploadAudio(StorageItem item, string mp3Location, ILogger log, CancellationToken cancellationToken)
        {
            log.LogInformation("Uploading to Blob Storage...");
            item.Message = string.Empty;
            item.ConversionDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            item.StorageUrl = await BlobStorageRepository.Upload(item.Id, mp3Location, cancellationToken);
            await BlobStorageRepository.Upload(item.Id, item, cancellationToken);
        }
    }
};