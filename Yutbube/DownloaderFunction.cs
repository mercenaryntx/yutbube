using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using Yutbube.Extensions;
using Yutbube.Models;
using Yutbube.Repositories;

namespace Yutbube
{
    public static class DownloaderFunction
    {
        private static readonly YoutubeClient YoutubeClient = new YoutubeClient();
        private static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("file:\\", string.Empty);

        private static readonly string TempDirectoryPath = Path.GetTempPath();
        private static readonly string OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "Output");

        private const string NumberFormat = @"[\d\.]+";
        private const string TimeFormat = @"[\d\.:]+";
        private static readonly string SizePart = $@"\s*(?<size>{NumberFormat})kB";
        private static readonly string TimePart = $@"\s*(?<time>{TimeFormat})";
        private static readonly string BitratePart = $@"\s*(?<bitrate>{NumberFormat})kbits\/s";
        private static readonly string SpeedPart = $@"\s*(?<speed>{NumberFormat})x";
        private static readonly string FfmegStatus = $@"size={SizePart}\s+time={TimePart}\s+bitrate={BitratePart}\s+speed={SpeedPart}";

        private const string PROCESSING = "Processing...";
        private const string DOWNLOADING = "Downloading...";
        private const string CONVERTING = "Converting...";

        [FunctionName("downloader")]
        public static async Task Run(
            [QueueTrigger("%AzureStorageConversionQueueName%", Connection = "AzureWebJobsStorage")]QueueMessagePayload payload,
            [SignalR(HubName = "broadcast")]IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation("Downloader triggered");
            var video = payload.Video;
            string videoTempPath = null;
            string audioTempPath = null;
            int? previousPercentage = null;
            try
            {
                video.Message = PROCESSING;
                signalRMessages.Publish(payload.ClientId, video);
                var streamInfo = await ProcessVideo(video, log);

                video.Message = DOWNLOADING;
                signalRMessages.Publish(payload.ClientId, video);
                videoTempPath = await DownloadVideo(streamInfo, log);

                video.Message = CONVERTING;
                signalRMessages.Publish(payload.ClientId, video);

                void ProgressNotifier(string s)
                {
                    var m = new Regex(FfmegStatus).Match(s);
                    if (m.Success && TimeSpan.TryParse(m.Groups["time"].Value, CultureInfo.InvariantCulture, out var time))
                    {
                        var p = (int)Math.Floor(time.TotalMilliseconds * 100 / video.Duration.TotalMilliseconds);
                        if (previousPercentage == p) return;
                        video.Message = $"{CONVERTING} ({p}%)";
                        signalRMessages.Publish(payload.ClientId, video);
                        previousPercentage = p;
                    }
                }

                audioTempPath = await ConvertToAudio(video, videoTempPath, log, ProgressNotifier);
                WriteId3Tag(video, audioTempPath, log);

                video.Message = "Storing...";
                signalRMessages.Publish(payload.ClientId, video);
                await UploadAudio(video, audioTempPath, log);
            }
            catch (Exception ex)
            {
                log.Log(LogLevel.Error, ex.Message, ex);
                video.Error = ex.Message;
            }
            finally
            {
                if (videoTempPath != null && File.Exists(videoTempPath)) File.Delete(videoTempPath);
                if (audioTempPath != null && File.Exists(audioTempPath)) File.Delete(audioTempPath);
                video.Message = string.Empty;
                signalRMessages.Publish(payload.ClientId, video);
            }
        }

        private static async Task<MediaStreamInfo> ProcessVideo(StorageItem video, ILogger log)
        {
            log.LogInformation($"Working on video [{video.Id}]...");

            var set = await YoutubeClient.GetVideoMediaStreamInfosAsync(video.Id);
            log.LogInformation($"{video.Title}");

            // Get highest bitrate audio-only or highest quality mixed stream
            return GetBestAudioStreamInfo(set);
        }

        private static async Task<string> DownloadVideo(MediaStreamInfo streamInfo, ILogger log)
        {
            // Download to temp file
            log.LogInformation("Downloading...");
            var streamFileExt = streamInfo.Container.GetFileExtension();
            var streamFilePath = Path.Combine(TempDirectoryPath, $"{Guid.NewGuid()}.{streamFileExt}");
            using (var streamFile = new FileStream(streamFilePath, FileMode.Create))
            {
                await YoutubeClient.DownloadMediaStreamAsync(streamInfo, streamFile);
            }
            return streamFilePath;
        }

        private static async Task<string> ConvertToAudio(StorageItem video, string tempPath, ILogger log, Action<string> progress)
        {
            // Convert to mp3
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
            return mp3Path;
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

        private static MediaStreamInfo GetBestAudioStreamInfo(MediaStreamInfoSet set)
        {
            if (set.Audio.Any()) return set.Audio.WithHighestBitrate();
            if (set.Muxed.Any()) return set.Muxed.WithHighestVideoQuality();
            throw new Exception("No applicable media streams found for this video");
        }
    }
}