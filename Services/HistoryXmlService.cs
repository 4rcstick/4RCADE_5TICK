using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // [SECTION: History.xml Service]
    // Loads and parses history.xml (arcade-history.com's community-curated game trivia/staff/technical
    // data) into a romname -> HistoryEntry lookup, feeding Column Two's Game Info/Trivia/Tips & Tricks
    // states and Column One Row 3's Staff/Technical states in the media panel rework.
    //
    // Looks for history_arcade.xml first (the pruned, arcade-only file produced by the project's
    // PowerShell trimming script - console/computer/handheld entries stripped, ~17.5MB vs the original
    // ~63MB), falling back to a plain history.xml if that trimmed file isn't present. This lets a user
    // drop in their own copy (full or differently-pruned) without being forced through the trim script
    // first - same fail-open philosophy already used elsewhere in this app (unrecognized ROMs passing
    // filters by default, etc.). Parsing logic is identical either way since the schema's the same;
    // the untrimmed fallback is just slower to load.
    //
    // Loaded once at startup (mirroring VirtualCategorySortService.EnsureSortDatabaseLoadedAsync's
    // pattern) rather than lazily on first game selection, since the file only needs parsing once per
    // session and startup is an acceptable place to absorb that cost.
    public class HistoryXmlService
    {
        private readonly ConfigurationSettings _settings;
        private Dictionary<string, HistoryEntry>? _cachedEntries;

        // [Stage 2: Stub Resolution]
        // _romToParent: every romname in the user's actual romset -> its resolved MAME parent (CloneOf,
        // or itself if it has none). Built from GameItem data via BuildLineageIndex, called from
        // MainViewModel once GamesCollection's data is available (history.xml itself has no clone
        // relationships of its own - it just lists romnames per entry).
        //
        // _parentToRealEntries: resolved parent -> distinct NON-stub HistoryEntry instances found under
        // any romname belonging to that parent family. A stub's real content is only auto-substituted
        // when this resolves to EXACTLY ONE candidate - real analysis of the actual history_arcade.xml +
        // mame_cache.json data showed some MAME clone families (e.g. Altered Beast's many regional/bootleg
        // variants) are deliberately written up as multiple DISTINCT arcade-history.com entries, so
        // sharing a CloneOf parent alone isn't a safe enough signal when more than one candidate exists -
        // picking arbitrarily there risks attaching the wrong write-up, worse than showing the stub.
        private Dictionary<string, string>? _romToParent;
        private Dictionary<string, List<HistoryEntry>>? _parentToRealEntries;

        // Manual escape hatch for stubs the automatic lineage search can't (or shouldn't) resolve on its
        // own - simple "stub_romname = target_romname" lines in history_overrides.cfg, checked before
        // the automatic resolution. Always wins when present, even over an unambiguous lineage match.
        private Dictionary<string, string>? _overrides;

        // Section headers this parser recognizes, in the exact "- NAME -" form they appear in the raw
        // text. Deliberately a fixed whitelist, not a generic "- [A-Z ]+ -" pattern - real history.xml
        // data has rare coincidental dash-wrapped fragments (e.g. "- C -" from a stray part number) that
        // a loose pattern would misfire on and split mid-sentence.
        private static readonly string[] KnownHeaders =
        {
            "- TECHNICAL -", "- TRIVIA -", "- STAFF -", "- TIPS AND TRICKS -",
            "- SCORING -", "- PORTS -", "- SERIES -", "- UPDATES -", "- CONTRIBUTE -",
            "- HAPPY BIRTHDAY -", "- CAST OF CHARACTERS -", "- FORBIDDEN PLAYING METHODS -"
        };

        public HistoryXmlService(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // [SECTION: Load]
        public async Task EnsureHistoryLoadedAsync()
        {
            if (_cachedEntries != null) return;

            await Task.Run(() =>
            {
                string databaseDir = Path.Combine(_settings.GetArcadeStickFilesPath(), _settings.ConfigSubFolder, "database");
                string trimmedPath = Path.Combine(databaseDir, "history_arcade.xml");
                string fullPath = Path.Combine(databaseDir, "history.xml");

                string? historyPath = File.Exists(trimmedPath) ? trimmedPath
                                     : File.Exists(fullPath) ? fullPath
                                     : null;

                var entries = new Dictionary<string, HistoryEntry>(StringComparer.OrdinalIgnoreCase);

                if (historyPath != null)
                {
                    ParseHistoryFile(historyPath, entries);
                }

                _cachedEntries = entries;

                string overridesPath = Path.Combine(databaseDir, "history_overrides.cfg");
                _overrides = LoadOverrides(overridesPath);
            });
        }

        // Re-reads history_overrides.cfg only, without touching the cached history.xml entries or the
        // lineage index - lets a manually-edited override take effect immediately (hotkey-triggered) 
        // instead of requiring a full app restart. Safe to call even if EnsureHistoryLoadedAsync hasn't
        // run yet; _overrides just stays whatever LoadOverrides returns (empty dict if the file's missing).
        public void ReloadOverrides()
        {
            string databaseDir = Path.Combine(_settings.GetArcadeStickFilesPath(), _settings.ConfigSubFolder, "database");
            string overridesPath = Path.Combine(databaseDir, "history_overrides.cfg");
            _overrides = LoadOverrides(overridesPath);
        }

        // Parses "stub_romname = target_romname" lines, skipping blanks and #-prefixed comments. Missing
        // file just means an empty override set - fail open, same convention as everything else here.
        private Dictionary<string, string> LoadOverrides(string path)
        {
            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return overrides;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                int equalsIndex = trimmed.IndexOf('=');
                if (equalsIndex <= 0) continue;

                string stubRom = trimmed.Substring(0, equalsIndex).Trim();
                string targetRom = trimmed.Substring(equalsIndex + 1).Trim();
                if (string.IsNullOrEmpty(stubRom) || string.IsNullOrEmpty(targetRom)) continue;

                overrides[stubRom] = targetRom;
            }

            return overrides;
        }

        // Builds the CloneOf-based lineage index used to auto-resolve unambiguous stub cases. Takes the
        // FULL, unfiltered romname -> parent map (CacheScannerService.GetFullRomToParentMapAsync) rather
        // than GamesCollection - the whole point of this index is to find a stub's real content via a
        // sibling regional release (e.g. sf2's Japanese sf2j) even when that sibling isn't physically
        // present in the user's romset, so it can't be built from what's actually on disk.
        public void BuildLineageIndex(Dictionary<string, string> fullRomToParentMap)
        {
            if (_cachedEntries == null) return;

            _romToParent = fullRomToParentMap;

            var parentToEntrySet = new Dictionary<string, HashSet<HistoryEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _cachedEntries)
            {
                if (kvp.Value.IsStub) continue; // only real entries are valid substitution targets
                if (!_romToParent.TryGetValue(kvp.Key, out var parent)) continue; // romname not in user's actual romset

                if (!parentToEntrySet.TryGetValue(parent, out var set))
                {
                    set = new HashSet<HistoryEntry>();
                    parentToEntrySet[parent] = set;
                }
                set.Add(kvp.Value); // reference equality naturally dedupes romnames sharing one entry instance
            }

            _parentToRealEntries = new Dictionary<string, List<HistoryEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parentToEntrySet)
            {
                _parentToRealEntries[kvp.Key] = new List<HistoryEntry>(kvp.Value);
            }
        }

        // Streams the file with XmlReader rather than loading a full DOM up front - keeps memory
        // reasonable even against an untrimmed ~63MB fallback file.
        private void ParseHistoryFile(string path, Dictionary<string, HistoryEntry> entries)
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = false
            };

            using var reader = XmlReader.Create(path, readerSettings);

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry")
                {
                    string entryXml = reader.ReadOuterXml();
                    ParseSingleEntry(entryXml, entries);
                }
            }
        }

        private void ParseSingleEntry(string entryXml, Dictionary<string, HistoryEntry> entries)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(entryXml);
            }
            catch
            {
                return; // Malformed entry fragment - skip rather than crash the whole load
            }

            var systemNodes = doc.SelectNodes("/entry/systems/system");
            if (systemNodes == null || systemNodes.Count == 0) return; // No arcade romnames on this entry

            string rawText = doc.SelectSingleNode("/entry/text")?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawText)) return;

            HistoryEntry parsedEntry = SplitEntryText(rawText);

            foreach (XmlNode systemNode in systemNodes)
            {
                string? romName = systemNode.Attributes?["name"]?.Value;
                if (string.IsNullOrEmpty(romName)) continue;

                // Confirmed via the trimmed file that romnames never repeat across entries, so a plain
                // assignment (not TryAdd) is safe and simplest.
                entries[romName] = parsedEntry;
            }
        }
        // [END SECTION: Load]

        // [SECTION: Section Splitting]
        // Splits one entry's raw <text> blob into named sections using the KnownHeaders whitelist.
        // Everything before the first recognized header becomes GameInfo (the free-text intro/description -
        // Column Two's "Game Info" state). CONTRIBUTE and any other unrecognized/uninteresting section is
        // parsed past but discarded, since only Trivia/TipsAndTricks/Staff/Technical are bound anywhere.
        private HistoryEntry SplitEntryText(string rawText)
        {
            var result = new HistoryEntry();

            // Normalize line endings so header matching doesn't have to account for \r\n vs \n
            string normalized = rawText.Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');

            string currentHeader = string.Empty; // empty = still in the intro, before any header
            var currentBuffer = new StringBuilder();

            void FlushCurrentSection()
            {
                string content = currentBuffer.ToString().Trim();
                currentBuffer.Clear();

                switch (currentHeader)
                {
                    case "": result.GameInfo = content; break;
                    case "- TRIVIA -": result.Trivia = content; break;
                    case "- TIPS AND TRICKS -": result.TipsAndTricks = content; break;
                    case "- STAFF -": result.Staff = content; break;
                    case "- TECHNICAL -": result.Technical = content; break;
                        // Everything else (SCORING/PORTS/SERIES/UPDATES/CONTRIBUTE/etc.) is intentionally dropped
                }
            }

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

                bool isKnownHeader = false;
                foreach (var header in KnownHeaders)
                {
                    if (trimmedLine == header)
                    {
                        isKnownHeader = true;
                        break;
                    }
                }

                if (isKnownHeader)
                {
                    FlushCurrentSection();
                    currentHeader = trimmedLine;
                }
                else
                {
                    currentBuffer.AppendLine(line);
                }
            }

            FlushCurrentSection(); // capture whatever section was still open at end-of-text

            // Strips arcade-history.com's boilerplate lead-in sentence ("Arcade Video game published 34
            // years ago:") off the front of GameInfo - always the same templated wording with just the
            // year count changing, so a regex anchored to the start handles every entry in one pass.

            // Lead-in varies by hardware/format ("Arcade Video game", "Sega ST-V cart.", "Taito G-Net
            // card", "Sega NAOMI 2 GD-ROM", etc.), but all variants end the same way - matching up through
            // "published N years ago:" (non-greedy, anchored to start) strips the boilerplate regardless
            // of which specific lead-in phrase precedes it.
            result.GameInfo = Regex.Replace(result.GameInfo, @"^.*?published \d+ years? ago:\s*", "", RegexOptions.IgnoreCase).Trim();

            // Next line is almost always "Title (c) Year Publisher." - redundant with the title header and
            // the Published/Year fields already shown elsewhere in the preview panel. Only stripped when
            // there's real content after it, though - some sparse entries have nothing else in GameInfo at
            // all, and showing this line beats showing a blank Game Info panel.
            var pubLineMatch = Regex.Match(result.GameInfo, @"^(?<publine>[^\n]*\(c\)\s*\d{4}[^\n]*)\n\s*(?<rest>.*)", RegexOptions.Singleline);
            if (pubLineMatch.Success && !string.IsNullOrWhiteSpace(pubLineMatch.Groups["rest"].Value))
            {
                result.GameInfo = pubLineMatch.Groups["rest"].Value.Trim();
            }
            // Stage 2 groundwork: flag stub entries now (cheap to compute here), even though nothing
            // acts on it yet - real lineage-validated substitution comes as a later pass.
            // "See"/"visit"..."original" phrasing varies across entries - sometimes adjacent ("see the
            // original"), sometimes with a year/publisher/possessive wedged between ("See the 1978
            // original standard version", "See Konami's original"), and the redirect verb itself isn't
            // always "see" (e.g. "please visit the original Technos Japan entry"). Allowing up to a
            // 4-word gap and either verb catches these without false-positiving on long descriptive
            // entries that happen to use "original" far from either verb (confirmed against known cases:
            // Gauntlet II, Gravitar, Mr. Do!, Bubble Bobble sequel, MK3).
            result.IsStub = Regex.IsMatch(result.GameInfo, @"\b(?:see|visit)\b(?:\s+\S+){0,4}?\s+\boriginal\b", RegexOptions.IgnoreCase);

            return result;
        }
        // [END SECTION: Section Splitting]

        // [SECTION: Public Lookup]
        // Resolution order for a stub match: manual override first (always wins when present) -> single
        // unambiguous CloneOf-lineage match -> the stub itself, displayed as-is. Only ever returns null
        // when romName isn't in history.xml at all - callers already have their own CloneOf fallback for
        // that case (retry with the parent romname), kept separate from this stub-specific logic.
        public HistoryEntry? GetHistoryEntry(string romName)
        {
            if (_cachedEntries == null || string.IsNullOrEmpty(romName)) return null;
            if (!_cachedEntries.TryGetValue(romName, out var entry)) return null;
            if (!entry.IsStub) return entry;

            if (_overrides != null && _overrides.TryGetValue(romName, out var overrideTarget)
                && _cachedEntries.TryGetValue(overrideTarget, out var overrideEntry))
            {
                return overrideEntry;
            }

            if (_romToParent != null && _parentToRealEntries != null
                && _romToParent.TryGetValue(romName, out var parent)
                && _parentToRealEntries.TryGetValue(parent, out var candidates)
                && candidates.Count == 1)
            {
                return candidates[0];
            }

            return entry; // ambiguous, unresolved, or no override - show the stub rather than guess
        }
        // [END SECTION: Public Lookup]
    }
    // [END SECTION: History.xml Service]
}