using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class ArtworkScraperService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string BaseUrl = "https://adb.arcadeitalia.net/service_scraper.php";

        private readonly ConfigurationSettings _settings;

        public ArtworkScraperService(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // Fetches and saves any enabled, missing artwork categories for a single game. Safe to call
        // for both the launch trigger and the context-menu trigger - both are single-game requests,
        // so no queueing/throttling logic is needed here (ADB's "one connection per IP" courtesy is
        // naturally respected since only one game is ever requested per call).
        //
        // Clone/parent handling: queries the game's own romname first. If the game is a clone
        // (CloneOf set) and any enabled category came back empty for the clone itself, a SECOND
        // query is made against the parent romname - but only then, not unconditionally, to avoid
        // doubling every clone's API traffic. Any category filled in from the parent's result is
        // saved under the PARENT's romname, not the clone's, so TryResolveMediaFile's existing
        // parent-fallback picks it up for every sibling clone too, and so this same fetch doesn't
        // re-download a duplicate copy per clone.
        public async Task FetchArtworkForGameAsync(GameItem game, CancellationToken ct = default)
        {
            if (!_settings.ScraperEnabled) return;
            if (game == null || string.IsNullOrWhiteSpace(game.RomName)) return;

            AdbMediaResult? ownMedia = await QueryMediaAsync(game.RomName, ct);

            bool hasParent = !string.IsNullOrWhiteSpace(game.CloneOf) &&
                              !game.CloneOf!.Equals(game.RomName, StringComparison.OrdinalIgnoreCase);

            bool anyOwnFieldMissing =
                (_settings.ScraperFetchMarquees && string.IsNullOrWhiteSpace(ownMedia?.MarqueeUrl)) ||
                (_settings.ScraperFetchFlyers && string.IsNullOrWhiteSpace(ownMedia?.FlyerUrl)) ||
                (_settings.ScraperFetchTitlescreens && string.IsNullOrWhiteSpace(ownMedia?.TitleScreenUrl)) ||
                (_settings.ScraperFetchSnaps && string.IsNullOrWhiteSpace(ownMedia?.GameplaySnapUrl)) ||
                (_settings.ScraperFetchCabinets && string.IsNullOrWhiteSpace(ownMedia?.CabinetUrl)) ||
                (_settings.ScraperFetchVideos && string.IsNullOrWhiteSpace(ownMedia?.VideoUrl));

            AdbMediaResult? parentMedia = (hasParent && anyOwnFieldMissing)
                ? await QueryMediaAsync(game.CloneOf!, ct)
                : null;

            var categoryTasks = new List<Task>
            {
                ProcessCategoryAsync(_settings.ScraperFetchMarquees, ownMedia?.MarqueeUrl, parentMedia?.MarqueeUrl, _settings.MarqueesPath, game, ".png", ct),
                ProcessCategoryAsync(_settings.ScraperFetchFlyers, ownMedia?.FlyerUrl, parentMedia?.FlyerUrl, _settings.FlyersPath, game, ".png", ct),
                ProcessCategoryAsync(_settings.ScraperFetchTitlescreens, ownMedia?.TitleScreenUrl, parentMedia?.TitleScreenUrl, _settings.TitlescreensPath, game, ".png", ct),
                ProcessCategoryAsync(_settings.ScraperFetchSnaps, ownMedia?.GameplaySnapUrl, parentMedia?.GameplaySnapUrl, _settings.ScreenshotsPath, game, ".png", ct),
                ProcessCategoryAsync(_settings.ScraperFetchCabinets, ownMedia?.CabinetUrl, parentMedia?.CabinetUrl, _settings.CabinetsPath, game, ".png", ct),
                ProcessCategoryAsync(_settings.ScraperFetchVideos, ownMedia?.VideoUrl, parentMedia?.VideoUrl, _settings.VideosPath, game, ".mp4", ct),
            };

            await Task.WhenAll(categoryTasks);
        }

        // Calls ADB's query_mame_media endpoint for a single romset and returns the first result, or
        // null if the game isn't in ADB's database or the request fails outright.
        private async Task<AdbMediaResult?> QueryMediaAsync(string romName, CancellationToken ct)
        {
            string url = $"{BaseUrl}?ajax=query_mame_media&game_name={Uri.EscapeDataString(romName)}";

            try
            {
                string json = await _httpClient.GetStringAsync(url, ct);
                var response = JsonSerializer.Deserialize<AdbMediaQueryResponse>(json);
                return response?.Result?.Count > 0 ? response.Result[0] : null;
            }
            catch
            {
                return null;
            }
        }

        // Calls ADB's download_status endpoint to check remaining bandwidth headroom. Returns null on
        // any failure (network, malformed response) so callers can distinguish "couldn't check" from
        // a genuine zero-remaining result.
        public async Task<AdbDownloadStatusResult?> CheckDownloadStatusAsync(CancellationToken ct = default)
        {
            string url = $"{BaseUrl}?ajax=download_status";

            try
            {
                string json = await _httpClient.GetStringAsync(url, ct);
                var response = JsonSerializer.Deserialize<AdbDownloadStatusResponse>(json);
                return response?.Result;
            }
            catch
            {
                return null;
            }
        }

        // Decides whether a category needs fetching at all (skips if a local file already exists
        // under either the game's own romname or its parent's - TryResolveMediaFile would already
        // find either one), then downloads from the game's own URL if available, falling back to the
        // parent's URL (saved under the PARENT's romname) if the clone has nothing of its own.
        private async Task ProcessCategoryAsync(bool enabled, string? ownUrl, string? parentUrl, string categorySubfolder, GameItem game, string extension, CancellationToken ct)
        {
            if (!enabled) return;

            string folder = _settings.GetMediaCategoryPath(categorySubfolder);

            if (!_settings.ScraperOverwriteExisting && LocalFileExists(folder, game.RomName, game.CloneOf, extension))
                return;

            if (!string.IsNullOrWhiteSpace(ownUrl))
            {
                await DownloadAndSaveAsync(ownUrl!, folder, game.RomName, extension, ct);
            }
            else if (!string.IsNullOrWhiteSpace(parentUrl) && !string.IsNullOrWhiteSpace(game.CloneOf))
            {
                await DownloadAndSaveAsync(parentUrl!, folder, game.CloneOf!, extension, ct);
            }
        }

        // Mirrors MainViewModel's TryResolveMediaFile check (primary romname, then fallback/parent
        // romname) but only needs a yes/no answer here, not the matched path itself.
        private static bool LocalFileExists(string folder, string primaryRomName, string? fallbackRomName, string extension)
        {
            if (File.Exists(Path.Combine(folder, $"{primaryRomName}{extension}"))) return true;
            if (!string.IsNullOrWhiteSpace(fallbackRomName) && File.Exists(Path.Combine(folder, $"{fallbackRomName}{extension}"))) return true;
            return false;
        }

        // Downloads a single asset and saves it as {saveAsRomName}.{extension} in the given folder.
        private async Task DownloadAndSaveAsync(string sourceUrl, string folder, string saveAsRomName, string extension, CancellationToken ct)
        {
            string destinationPath = Path.Combine(folder, $"{saveAsRomName}{extension}");

            try
            {
                Directory.CreateDirectory(folder);
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(sourceUrl, ct);
                await File.WriteAllBytesAsync(destinationPath, fileBytes, ct);
            }
            catch
            {
                // Best-effort - a single failed asset shouldn't block the others in Task.WhenAll
            }
        }
    }
}