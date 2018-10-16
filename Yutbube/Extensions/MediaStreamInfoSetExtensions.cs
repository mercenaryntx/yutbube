using System;
using System.Linq;
using YoutubeExplode.Models.MediaStreams;

namespace Yutbube.Extensions
{
    public static class MediaStreamInfoSetExtensions
    {
        public static MediaStreamInfo GetBestAudioStreamInfo(this MediaStreamInfoSet set)
        {
            if (set.Audio.Any()) return set.Audio.WithHighestBitrate();
            if (set.Muxed.Any()) return set.Muxed.WithHighestVideoQuality();
            throw new Exception("No applicable media streams found for this video");
        }
    }
}