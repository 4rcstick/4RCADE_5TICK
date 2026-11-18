using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // [SECTION: Random Video Previews Pool]
    // Random Video Previews (random-video-previews-handoff.md, themes.md 3.31): which games of the filtered Game Pool have a
    // gameplay video, and the order they play in. Owned by MainWindow; nothing here touches the UI, the Game Pool or the
    // selection, and nothing is shared with the normal Video Preview or with a future Attract Mode.

    // One playable entry: the Game Pool's own game (its title and ROM are what the layer shows and launches) and the video file
    // found for it, which may be its clone parent's.
    public sealed record RandomPreviewCandidate(GameItem Game, string VideoPath);

    public static class RandomPreviewPool
    {
        // The video file types the normal preview lookup accepts, in its order
        private static readonly string[] VideoExtensions = { ".mp4", ".avi" };

        // Does the eligibility scan (run on a background thread): lists the videos folder once, then finds each pool game's
        // video in memory with the normal lookup's rules for video files only, ignoring Preview Priority: the game's own ROM
        // name first (.mp4, then .avi), then its clone parent's. A missing or unreadable folder gives an empty list.
        public static List<RandomPreviewCandidate> BuildEligible(IReadOnlyList<GameItem> poolGames, string videosFolder)
        {
            var result = new List<RandomPreviewCandidate>();
            if (poolGames.Count == 0 || string.IsNullOrWhiteSpace(videosFolder)) return result;

            var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!Directory.Exists(videosFolder)) return result;
                foreach (string file in Directory.EnumerateFiles(videosFolder))
                {
                    string extension = Path.GetExtension(file);
                    if (VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) fileNames.Add(Path.GetFileName(file));
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"RandomPreviewPool: could not list '{videosFolder}': {exception.Message}");
                return result;
            }
            if (fileNames.Count == 0) return result;

            var seenRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in poolGames)
            {
                if (game == null || string.IsNullOrEmpty(game.RomName) || !seenRoms.Add(game.RomName)) continue;

                string? video = FindVideo(fileNames, game.RomName) ?? (string.IsNullOrEmpty(game.CloneOf) ? null : FindVideo(fileNames, game.CloneOf));
                if (video != null) result.Add(new RandomPreviewCandidate(game, Path.Combine(videosFolder, video)));
            }
            return result;
        }

        // Does one name's lookup: the first video extension that exists for that ROM name, or null
        private static string? FindVideo(HashSet<string> fileNames, string romName)
        {
            foreach (string extension in VideoExtensions)
            {
                string name = romName + extension;
                if (fileNames.Contains(name)) return name;
            }
            return null;
        }
    }

    // Does the playback order over the eligible list: Shuffle plays every game once before reshuffling, never starting a round
    // with the game that just played; Random picks freely each time, never the game that just played. Both play one game on its
    // own and alternate two. A game that fails to play is left out until the next pool. A new pool starts a new shuffle round.
    public sealed class RandomPreviewSequencer
    {
        private readonly Random _random = new();
        private List<RandomPreviewCandidate> _pool = new();
        private readonly HashSet<string> _failedRoms = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<RandomPreviewCandidate> _bag = new();

        // True when at least one game can still play
        public bool HasCandidates => _pool.Any(c => !_failedRoms.Contains(c.Game.RomName));

        // True when a game other than "current" can play next (the NEXT control shows only then)
        public bool HasAlternativeTo(RandomPreviewCandidate? current) =>
            _pool.Any(c => !_failedRoms.Contains(c.Game.RomName) && !SameRom(c, current?.Game.RomName));

        // Does the pool swap: the new eligible list, a cleared failure list and a fresh shuffle round
        public void SetPool(List<RandomPreviewCandidate> pool)
        {
            _pool = pool;
            _failedRoms.Clear();
            _bag.Clear();
        }

        // Does the failure rule: the game is skipped until the next pool
        public void MarkFailed(RandomPreviewCandidate candidate)
        {
            _failedRoms.Add(candidate.Game.RomName);
        }

        // Does the next pick after "previous" (the game that just played, or null): null when nothing can play. With only one
        // game left it returns that game, even when it is "previous".
        public RandomPreviewCandidate? Next(RandomPreviewCandidate? previous, string playbackOrder)
        {
            var available = _pool.Where(c => !_failedRoms.Contains(c.Game.RomName)).ToList();
            if (available.Count == 0) return null;
            if (available.Count == 1) return available[0];

            string? previousRom = previous?.Game.RomName;
            if (playbackOrder == LayerRegistry.PlaybackOrderRandom)
            {
                var choices = available.Where(c => !SameRom(c, previousRom)).ToList();
                if (choices.Count == 0) choices = available;
                return choices[_random.Next(choices.Count)];
            }

            // Shuffle: take from the current round, skipping games that failed since it was dealt
            while (_bag.Count > 0)
            {
                var next = _bag.Dequeue();
                if (_failedRoms.Contains(next.Game.RomName) || SameRom(next, previousRom)) continue;
                return next;
            }

            // Round over: deal a new one that does not start with the game that just played
            var round = available.OrderBy(_ => _random.Next()).ToList();
            if (SameRom(round[0], previousRom))
            {
                int swap = 1 + _random.Next(round.Count - 1);
                (round[0], round[swap]) = (round[swap], round[0]);
            }
            foreach (var candidate in round.Skip(1)) _bag.Enqueue(candidate);
            return round[0];
        }

        // Does the identity check by the Game Pool entry's ROM name
        private static bool SameRom(RandomPreviewCandidate candidate, string? romName) =>
            romName != null && string.Equals(candidate.Game.RomName, romName, StringComparison.OrdinalIgnoreCase);
    }
    // [END SECTION: Random Video Previews Pool]
}
