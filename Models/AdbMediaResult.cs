using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ArcadeStick.Models
{
    // Deserialization target for ADB's QUERY_MAME_MEDIA response. Confirmed against a live call
    // (mspacman, 2026) - the response is always wrapped in this envelope, with "result" as an array
    // even for a single romset query.
    public class AdbMediaQueryResponse
    {
        [JsonPropertyName("release")]
        public int Release { get; set; }

        [JsonPropertyName("result")]
        public List<AdbMediaResult> Result { get; set; } = new();
    }

    // Deserialization target for ADB's DOWNLOAD_STATUS response. Confirmed against a live call (2026) -
    // just two flat numbers under "result", simpler than the tiered ip/daily/weekly/monthly breakdown
    // the documentation implies.
    public class AdbDownloadStatusResponse
    {
        [JsonPropertyName("release")]
        public int Release { get; set; }

        [JsonPropertyName("result")]
        public AdbDownloadStatusResult? Result { get; set; }
    }

    public class AdbDownloadStatusResult
    {
        [JsonPropertyName("download_limit_files")]
        public long DownloadLimitFiles { get; set; }

        [JsonPropertyName("download_limit_bytes")]
        public long DownloadLimitBytes { get; set; }
    }

    public class AdbMediaResult
    {
        [JsonPropertyName("game_name")]
        public string? RomName { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("cloneof")]
        public string? CloneOf { get; set; }

        [JsonPropertyName("url_image_flyer")]
        public string? FlyerUrl { get; set; }

        [JsonPropertyName("url_image_title")]
        public string? TitleScreenUrl { get; set; }

        [JsonPropertyName("url_image_ingame")]
        public string? GameplaySnapUrl { get; set; }

        [JsonPropertyName("url_image_marquee")]
        public string? MarqueeUrl { get; set; }

        [JsonPropertyName("url_image_cabinet")]
        public string? CabinetUrl { get; set; }

        [JsonPropertyName("url_video_shortplay")]
        public string? VideoUrl { get; set; }
    }
}