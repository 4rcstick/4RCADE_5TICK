using ArcadeStick.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ArcadeStick.Services
{
    public class VirtualCategorySortService
    {
        private readonly ConfigurationSettings _settings;
        // General-purpose manual exclusion list (rom_exclusion_list.cfg) - originally scoped to
        // player-count variants specifically, broadened to a general escape hatch for excluding any ROM
        // by name (bootlegs that slip past the keyword denylist, personal taste, etc.).
        private readonly HashSet<string> _romExclusions;

        // In-memory cache of the parsed sort_database.ini - populated once per session via
        // EnsureSortDatabaseLoadedAsync, reused across toggle on/off and tree rebuilds rather than
        // re-parsing the ini file every time. Not persisted to disk. Clone/variant discovery no longer
        // comes from this ini (that field was a machine-generated snapshot that could drift out of sync)
        // - it's now found live via GameItem.CloneOf, sourced fresh from mame_cache.json every time.
        private Dictionary<string, MasterLibraryEntry>? _cachedParentLookup;
        private Dictionary<string, string>? _cachedVariantToParent;

        // Keywords that disqualify a clone/variant from appearing in the virtual sort tree - bootlegs,
        // hacks, and prototypes are noise for browsing purposes even though they may still be physically
        // present in the user's ROM set. Checked against the clone's full description (mame_cache.json).
        private static readonly string[] DisqualifyingKeywords = { "bootleg", "hack", "prototype" };

        // Maps the free-text region words found in MAME descriptions to the app's 5 filter buckets
        // (World/US/Japan/Asia/Europe). MAME's vocabulary isn't standardized (US vs USA, German vs
        // Germany), so this table absorbs known synonyms. Not exhaustive - unmapped/untagged entries are
        // treated as unspecified and pass region filtering by default rather than being excluded for a
        // property they don't declare.

        // Strict rule: ONLY the literal continent/region name itself keeps its own bucket (World, US,
        // Japan, Asia, Europe) - plus two confirmed real-volume exceptions where a single country carries
        // enough weight in the curated ROM set to warrant staying put (Korea within Asia, Germany/UK
        // within Europe, UK specifically because MAME convention often uses it as a stand-in for "the
        // European release" generally). Every other individual country - however many clones use it -
        // buckets into "Other" rather than accumulating its own exception, which would just recreate
        // sub-continent buckets by another name and defeat the point of narrowing at all.
        private static readonly Dictionary<string, string> RegionSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            { "World", "World" },
            { "US", "US" }, { "USA", "US" },
            { "Japan", "Japan" }, { "Japanese", "Japan" },
            { "Europe", "Europe" }, { "European", "Europe" }, { "UK", "Europe" },
            { "Germany", "Europe" }, { "German", "Europe" },
            { "Asia", "Asia" }, { "Korea", "Asia" },
            { "France", "Other" }, { "Italy", "Other" }, { "Spain", "Other" }, { "Netherlands", "Other" },
            { "Dutch", "Other" }, { "Ukraine", "Other" }, { "Latvia", "Other" }, { "Sweden", "Other" },
            { "Norway", "Other" }, { "Denmark", "Other" }, { "Poland", "Other" }, { "Portugal", "Other" },
            { "Greece", "Other" }, { "Belgium", "Other" }, { "Austria", "Other" }, { "Switzerland", "Other" },
            { "Russia", "Other" }, { "Finland", "Other" }, { "China", "Other" }, { "Taiwan", "Other" },
            { "Hong Kong", "Other" }, { "Australia", "Other" }, { "New Zealand", "Other" }, { "Oceania", "Other" },
            { "Canada", "Other" }, { "Mexico", "Other" }, { "Brazil", "Other" }, { "Hispanic", "Other" },
            { "Export", "Other" }, { "Czech", "Other" }, { "Slovakia", "Other" }, { "Hungary", "Other" },
            { "Argentina", "Other" }, { "Venezuela", "Other" }, { "Peru", "Other" }, { "Israel", "Other" },
            { "NSW", "Other" }, { "ACT", "Other" }
        };

        // Tightened revision pattern - requires a word boundary immediately after "rev"/"revision" so
        // title words like "Revenge" or "Revolution" don't false-positive (a naive "rev.*" match would
        // catch both), plus a "vN" version-number style (e.g. "V1.08"). "set N" was deliberately NOT
        // added here despite being the most common clone marker in the dataset - it carries no actual
        // differentiating information (doesn't say what changed), so it behaves better as an
        // undifferentiated/untagged entry than as a genuine revision.

        // RevisionPattern retired - replaced by ClassifyDescriptionParts's by-elimination approach below.
        // The old fixed keyword list ("Rev"/"Revision"/"vN"/"older"/"newer") only recognized specific
        // phrasings and missed most of what MAME's descriptions actually use (date stamps, "First
        // Version", "set N", licensee names, etc.) - now ANY descriptive text surviving after Region and
        // Player-Count removal counts as a revision, by definition, with no keyword list to maintain.

        // Filter state passed into BuildVirtualTree - kept as a plain options object rather than a field
        // on this service so the service stays stateless with respect to UI state. Empty collections mean
        // "no restriction" (show all) for that row, matching the checkbox-row semantics: checking nothing
        // behaves the same as checking everything.
        public class SortFilterOptions
        {
            // Master switch for the entire filtering engine - when false, every eligibility check
            // (keyword/exclusion-list gate, region, revision, player count, rating) short-circuits to
            // "pass", regardless of what else is set on this object. This is what makes "Enable Filters"
            // unchecked mean "show everything, completely unfiltered" rather than a partial filter state.
            public bool FilteringEnabled { get; set; }

            public HashSet<string> EnabledRegions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<int> EnabledPlayerCounts { get; set; } = new();
            public HashSet<string> EnabledGenres { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IncludeRevisions { get; set; }

            // Year range - unlike Rating/Category (curated in sort_database.ini, parent-only), Year comes
            // straight from -listxml per GameItem, so it's checked per-game in IsVariantEligible below,
            // not via a parent-lookup method. Defaults span the full slider range (1970-2026) so an
            // untouched filter behaves as "no restriction," same as MinimumRating/MaximumRating's defaults.
            public int MinimumYear { get; set; } = 1970;
            public int MaximumYear { get; set; } = 2026;

            // Single-select manufacturer filter - empty/null means no restriction, matching every other
            // filter's "unset = show everything" semantics. A game's actual Manufacturer string just
            // needs to CONTAIN this value (case-insensitive) rather than match exactly, so checking
            // "Atari" also catches "Atari Games", "Taito" catches "Taito Corporation Japan", etc.,
            // without needing a maintained synonym table the way RegionSynonyms requires.
            public string? SelectedManufacturer { get; set; }

            // Rating range (0-100, in increments of 10) a game's family's band must overlap to be shown.
            // Clones inherit their parent's band - there's no independent per-clone rating - so this is
            // always evaluated against the family's parent regardless of which member (parent or clone)
            // is being checked. See IsGameEligible.
            public int MinimumRating { get; set; } = 0;
            public int MaximumRating { get; set; } = 100;
        }

        // Parses a bracket section header (e.g. "70 to 80 (Great Games)") into its numeric band. Returns
        // null for headers that don't match the pattern (e.g. "FOLDER_SETTINGS"), so the caller can leave
        // the previously-active band untouched rather than resetting it.
        private static (int Min, int Max)? ParseRatingBand(string sectionHeader)
        {
            var match = Regex.Match(sectionHeader, @"(\d+)\s*to\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            if (!int.TryParse(match.Groups[1].Value, out int min)) return null;
            if (!int.TryParse(match.Groups[2].Value, out int max)) return null;
            return (min, max);
        }

        // Extracts the set of recognized region buckets from a clone's raw parenthetical text. Returns
        // an empty set if no known region word is found (treated as "unspecified" by IsVariantEligible).
        private static HashSet<string> ParseRegions(string rawParenthetical)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in RegionSynonyms)
            {
                if (Regex.IsMatch(rawParenthetical, $@"\b{Regex.Escape(kvp.Key)}\b", RegexOptions.IgnoreCase))
                {
                    found.Add(kvp.Value);
                }
            }
            return found;
        }

        // Splits a clone's raw parenthetical text into region-word parts and "residual" parts (anything
        // else surviving noise removal). Single source of truth for BOTH the differentiator display text
        // AND revision-tagged detection - handles combined phrasing like "UK 4 Players" or
        // "Taiwan / Japan" by extracting region words and player-count text from WITHIN a part, not just
        // parts that are purely one or the other.
        private static (List<string> RegionParts, List<string> ResidualParts) ClassifyDescriptionParts(string rawParenthetical)
        {
            var regionParts = new List<string>();
            var residualParts = new List<string>();

            if (string.IsNullOrEmpty(rawParenthetical)) return (regionParts, residualParts);

            var groups = Regex.Matches(rawParenthetical, @"\(([^()]*)\)");
            foreach (Match group in groups)
            {
                foreach (var rawPart in group.Groups[1].Value.Split(','))
                {
                    string part = rawPart.Trim();
                    if (string.IsNullOrEmpty(part)) continue;

                    foreach (var kvp in DisplayNormalization)
                    {
                        part = Regex.Replace(part, $@"\b{Regex.Escape(kvp.Key)}\b", kvp.Value, RegexOptions.IgnoreCase);
                    }

                    part = NoiseEmbeddedDateCode.Replace(part, "").Trim();
                    part = Regex.Replace(part, @"\s{2,}", " ");
                    if (string.IsNullOrEmpty(part)) continue;

                    // Whole-part noise is discarded entirely - neither region, player count, nor a
                    // meaningful revision marker.
                    if (NoiseDateCode.IsMatch(part)) continue;
                    if (NoisePcbHardware.IsMatch(part)) continue;
                    if (NoiseChipCode.IsMatch(part)) continue;

                    var regionWordsFound = new List<string>();
                    string remainder = part;
                    foreach (var rawWord in RegionSynonyms.Keys)
                    {
                        if (Regex.IsMatch(remainder, $@"\b{Regex.Escape(rawWord)}\b", RegexOptions.IgnoreCase))
                        {
                            string display = DisplayNormalization.TryGetValue(rawWord, out var normalized) ? normalized : rawWord;
                            if (!regionWordsFound.Contains(display, StringComparer.OrdinalIgnoreCase))
                                regionWordsFound.Add(display);
                            remainder = Regex.Replace(remainder, $@"\b{Regex.Escape(rawWord)}\b", "", RegexOptions.IgnoreCase);
                        }
                    }

                    remainder = Regex.Replace(remainder, @"\b\d+\s*Players?\b", "", RegexOptions.IgnoreCase);
                    remainder = Regex.Replace(remainder, @"[\/,]+", " ");
                    remainder = Regex.Replace(remainder, @"\s{2,}", " ").Trim();

                    foreach (var word in regionWordsFound)
                    {
                        if (!regionParts.Contains(word, StringComparer.OrdinalIgnoreCase))
                            regionParts.Add(word);
                    }

                    if (!string.IsNullOrEmpty(remainder))
                    {
                        residualParts.Add(remainder);
                    }
                }
            }

            return (regionParts, residualParts);
        }

        // True if a clone has any meaningful descriptive text beyond its Region and Player-Count tags -
        // see ClassifyDescriptionParts. A plain regional clone (description is ONLY a region tag) is
        // never revision-tagged; something extra (a date stamp, "First Version", "set 2", "Rev A",
        // anything) is what makes it one.
        private static bool IsRevisionTagged(string rawParenthetical)
        {
            var (_, residualParts) = ClassifyDescriptionParts(rawParenthetical);
            return residualParts.Count > 0;
        }

        // Normalizes a couple of verbose region words to their short display form. Deliberately separate
        // from RegionSynonyms's bucket mapping - display should keep countries distinct (Brazil stays
        // "Brazil" even though it buckets into "Other" for filtering purposes) so clones that bucket
        // together don't look identical again.
        private static readonly Dictionary<string, string> DisplayNormalization = new(StringComparer.OrdinalIgnoreCase)
        {
            { "USA", "US" }
        };

        // Noise patterns confirmed via a real scan of clone descriptions - things that carry no
        // meaningful information to a player, distinct from genuinely descriptive text like "First
        // Version" or "Williams Electronics license" (which should be KEPT, not discarded).
        private static readonly Regex NoiseDateCode = new(@"^\d{5,}$", RegexOptions.Compiled);
        private static readonly Regex NoiseEmbeddedDateCode = new(@"\b\d{5,}\b", RegexOptions.Compiled);
        private static readonly Regex NoisePcbHardware = new(@"\b(PCB|hardware)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NoiseChipCode = new(@"^[A-Z]{2,8}\d{1,4}$|^[A-Z]{1,4}-\d{1,4}$", RegexOptions.Compiled);
        private static readonly Regex DefaultTwoPlayer = new(@"^2\s*Players?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Extracts an embedded MAME production date code (YYMMDD format, e.g. "960619") for tiebreaking
        // purposes - reuses the same pattern NoiseEmbeddedDateCode strips for display. Numeric comparison
        // works directly since the digits are already most-significant-first, no real date parsing needed.
        // Returns -1 if no such code is present.
        private static int ExtractDateCode(string rawParenthetical)
        {
            var match = NoiseEmbeddedDateCode.Match(rawParenthetical);
            return match.Success && int.TryParse(match.Value, out int code) ? code : -1;
        }

        // Picks a single winner among multiple clones that all satisfy the same checked region - MAME's
        // clone tree often has several regional builds differing only by an internal production date
        // (e.g. three separate Japan revisions of the same release), which would otherwise all display
        // near-identically once cleaned for display. Prefers the most recent embedded date code; falls
        // back to alphabetical by ROM name when no candidate has one (a real possibility - not every
        // clone embeds a date).
        private static GameItem? PickRegionWinner(List<GameItem> candidates)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            var withDates = candidates
                .Select(c => (Game: c, Date: ExtractDateCode(c.RawParentheticalInfo)))
                .Where(x => x.Date >= 0)
                .OrderByDescending(x => x.Date)
                .ToList();

            if (withDates.Count > 0)
                return withDates[0].Game;

            return candidates.OrderBy(c => c.RomName, StringComparer.OrdinalIgnoreCase).First();
        }

        // [SECTION: Shared Family Visibility Resolution]
        // Single source of truth for "which ROMs should be visible", shared by BuildVirtualTree (catver
        // mode) and MainViewModel's custom-folder branch - guarantees both structure modes can never
        // disagree with each other on filtering/dedup behavior, since there's only one implementation.
        // Returns the complete set of ROM names (parents AND winning clones) that should display. When
        // filtering is off, every ROM is visible - safe to call unconditionally regardless of toggle state.
        public HashSet<string> ResolveVisibleFamilyMembers(IEnumerable<GameItem> games, SortFilterOptions filterOptions)
        {
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gamesList = games as IList<GameItem> ?? games.ToList();

            if (!filterOptions.FilteringEnabled)
            {
                foreach (var g in gamesList)
                {
                    if (!_romExclusions.Contains(g.RomName))
                        visible.Add(g.RomName);
                }
                return visible;
            }

            if (_cachedParentLookup == null) return visible;

            var cloneChildrenByParent = gamesList
                .Where(g => !string.IsNullOrEmpty(g.CloneOf))
                .GroupBy(g => g.CloneOf, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(grp => grp.Key, grp => grp.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var game in gamesList)
            {
                string romName = game.RomName.ToLowerInvariant();
                if (!_cachedParentLookup.ContainsKey(romName)) continue;

                bool parentEligible = IsGameEligible(game, filterOptions);
                cloneChildrenByParent.TryGetValue(romName, out var clones);
                var eligibleClones = clones?.Where(c => IsGameEligible(c, filterOptions)).ToList() ?? new List<GameItem>();

                // Parent-region shortcut: if the eligible parent itself matches a checked region, any
                // clone sharing that same region is redundant and gets dropped before dedup runs.
                var parentCoveredRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (parentEligible && filterOptions.EnabledRegions.Count > 0)
                {
                    parentCoveredRegions = ParseRegions(game.RawParentheticalInfo);
                    parentCoveredRegions.IntersectWith(filterOptions.EnabledRegions);
                }

                var clonesToConsider = eligibleClones.Where(c =>
                {
                    if (parentCoveredRegions.Count == 0) return true;
                    var cloneRegions = ParseRegions(c.RawParentheticalInfo);
                    return !cloneRegions.Overlaps(parentCoveredRegions);
                });

                // Dedup pass: group remaining clones by (title + final display text) and keep only the
                // tiebreaker winner from each colliding group - unique entries pass through untouched.
                // Grouping on title too prevents two clones with genuinely DIFFERENT titles from ever
                // being mistaken for duplicates just because their differentiator text happens to match.
                var winningClones = clonesToConsider
                    .GroupBy(c => $"{c.FullTitle}|{BuildCloneDifferentiator(c)}", StringComparer.OrdinalIgnoreCase)
                    .Select(grp => PickRegionWinner(grp.ToList()))
                    .Where(c => c != null);

                if (parentEligible)
                {
                    visible.Add(romName);
                }

                foreach (var winner in winningClones)
                {
                    visible.Add(winner!.RomName);
                }
            }

            return visible;
        }

        // Builds the pool for the "Play Random Game" feature - always evaluates as if the user's Base
        // Filter checkbox were on, regardless of its actual current state, using whatever region/rating/
        // revision/player-count values are currently saved in Configuration. This guarantees the die can
        // never land on a bootleg/hack/prototype/untracked ROM, independent of what the user currently
        // has their browsing view configured to show. Returns visible rom names, same shape as
        // ResolveVisibleFamilyMembers, since the caller (MainViewModel) still needs to map these back to
        // actual GameItem instances from GamesCollection.
        public HashSet<string> GetRandomizerPool(IEnumerable<GameItem> games, SortFilterOptions currentFilterOptions)
        {
            var forcedOptions = new SortFilterOptions
            {
                FilteringEnabled = true,
                EnabledRegions = currentFilterOptions.EnabledRegions,
                EnabledPlayerCounts = currentFilterOptions.EnabledPlayerCounts,
                EnabledGenres = currentFilterOptions.EnabledGenres,
                IncludeRevisions = currentFilterOptions.IncludeRevisions,
                MinimumRating = currentFilterOptions.MinimumRating,
                MaximumRating = currentFilterOptions.MaximumRating,
                MinimumYear = currentFilterOptions.MinimumYear,
                MaximumYear = currentFilterOptions.MaximumYear,
                SelectedManufacturer = currentFilterOptions.SelectedManufacturer
            };

            return ResolveVisibleFamilyMembers(games, forcedOptions);
        }
        // [END SECTION: Shared Family Visibility Resolution]

        // Builds a clean differentiator suffix for a clone, using the same ClassifyDescriptionParts split
        // that drives revision detection - region words, then any residual descriptive text (revision
        // markers, hardware/licensee variant names like "First Version", date stamps, "set N", etc.),
        // then a non-default player count from the structured Players field. Consolidates what used to be
        // multiple separate "(...)" groups into one clean set. A clone with nothing recognized in any
        // category returns an empty string, falling back to the plain title.
        public static string BuildCloneDifferentiator(GameItem clone)
        {
            var (regionParts, residualParts) = ClassifyDescriptionParts(clone.RawParentheticalInfo);

            var displayParts = new List<string>();
            displayParts.AddRange(regionParts);
            displayParts.AddRange(residualParts);

            if (clone.Players > 0 && clone.Players != 2)
            {
                displayParts.Add($"{clone.Players} Player{(clone.Players == 1 ? "" : "s")}");
            }

            return displayParts.Count > 0 ? $"({string.Join(", ", displayParts)})" : string.Empty;
        }

        // [SECTION: Lifecycle & Dependency Injection]
        // Stores the shared ConfigurationSettings instance and loads the manual ROM exclusion list
        // (rom_exclusion_list.cfg) - a small, user-editable list of ROM short names that get excluded
        // from the sorted tree unconditionally, for anything the automatic keyword/pattern filtering
        // doesn't catch (or shouldn't be expected to).
        public VirtualCategorySortService(ConfigurationSettings settings)
        {
            _settings = settings;
            _romExclusions = LoadRomExclusions();
        }

        private HashSet<string> LoadRomExclusions()
        {
            var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(_settings.GetConfigPath(), "rom_exclusion_list.cfg");

            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path)
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"));

                foreach (var rom in lines)
                {
                    exclusions.Add(rom);
                }
            }

            return exclusions;
        }

        // Adds a ROM to the manual exclusion list: appends it to rom_exclusion_list.cfg on disk (if not
        // already present) and updates the already-loaded in-memory _romExclusions cache in the same
        // call. This instance is long-lived (constructed once in MainViewModel's constructor), so
        // without updating the cache here too, an added exclusion wouldn't take effect until restart.
        public void AddRomExclusion(string romName)
        {
            if (string.IsNullOrWhiteSpace(romName)) return;
            if (_romExclusions.Contains(romName)) return;

            string path = Path.Combine(_settings.GetConfigPath(), "rom_exclusion_list.cfg");
            File.AppendAllLines(path, new[] { romName });

            _romExclusions.Add(romName);
        }
        // [END SECTION: Lifecycle & Dependency Injection]

        // [SECTION: sort_database.ini Parser]
        // Ensures the ini has been parsed and cached in-memory. Safe to call repeatedly - only parses
        // once per app session unless ForceReload is used.
        public async Task EnsureSortDatabaseLoadedAsync(bool forceReload = false)
        {
            if (!forceReload && _cachedParentLookup != null)
                return;

            _cachedParentLookup = await ParseSortDatabaseAsync();
        }

        // Parses sort_database.ini into a ParentROM -> MasterLibraryEntry lookup. Section headers
        // ([0 to 10 (Worst)], etc.) are parsed into a numeric rating band that gets stamped onto every
        // entry parsed underneath them, until the next section header changes it - except
        // [FOLDER_SETTINGS], which is skipped entirely since its two lines (RootFolderIcon/SubFolderIcon)
        // aren't real ROM data.
        private async Task<Dictionary<string, MasterLibraryEntry>> ParseSortDatabaseAsync()
        {
            var parentLookup = new Dictionary<string, MasterLibraryEntry>(StringComparer.OrdinalIgnoreCase);

            string iniPath = Path.Combine(_settings.GetConfigPath(), "database", "sort_database.ini");
            if (!File.Exists(iniPath))
            {
                return parentLookup;
            }

            string currentSection = string.Empty;
            int currentRatingMin = 0;
            int currentRatingMax = 100;

            using (var fileStream = new FileStream(iniPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var reader = new StreamReader(fileStream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    string trimmed = line.Trim();

                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Trim('[', ']');
                        var band = ParseRatingBand(currentSection);
                        if (band != null)
                        {
                            currentRatingMin = band.Value.Min;
                            currentRatingMax = band.Value.Max;
                        }
                        continue;
                    }

                    // FOLDER_SETTINGS holds icon defaults, not ROM data - skip its lines entirely
                    if (string.Equals(currentSection, "FOLDER_SETTINGS", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entry = ParseEntryLine(trimmed);
                    if (entry == null) continue;

                    entry.RatingMin = currentRatingMin;
                    entry.RatingMax = currentRatingMax;

                    parentLookup[entry.RomName] = entry;
                }
            }

            return parentLookup;
        }

        // Parses a single "parentrom = Score:x | Category:y | Variants:csv" line into a MasterLibraryEntry
        private MasterLibraryEntry? ParseEntryLine(string line)
        {
            int eqIndex = line.IndexOf('=');
            if (eqIndex < 0) return null;

            string romName = line.Substring(0, eqIndex).Trim().ToLowerInvariant();
            string fieldsPart = line.Substring(eqIndex + 1).Trim();

            var entry = new MasterLibraryEntry { RomName = romName };
            var segments = fieldsPart.Split('|');

            foreach (var segment in segments)
            {
                string seg = segment.Trim();
                int colonIndex = seg.IndexOf(':');
                if (colonIndex < 0) continue;

                string key = seg.Substring(0, colonIndex).Trim();
                string value = seg.Substring(colonIndex + 1).Trim();

                if (key.Equals("Score", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Score = value;
                }
                else if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Category = value;
                }
                // Variants field is no longer parsed - superseded by GameItem.CloneOf (see BuildVirtualTree).
                // Any Variants:CSV segment still present in an old sort_database.ini is silently ignored here.
            }

            return entry;
        }
        // [END SECTION: sort_database.ini Parser]

        // [SECTION: Clone/Variant Eligibility Filter]
        // Discrete, swappable eligibility check - kept separate from tree-building so a future
        // user-override list can slot in ahead of (or instead of) these automatic rules without
        // touching BuildVirtualTree. Checks the hardcoded player-count exclusion list and disqualifying
        // keywords first (cheap, always-on gates regardless of filter state), then evaluates the clone
        // against the active Sorting window filter state - region, player count, and revision-inclusion.
        public bool IsVariantEligible(GameItem game, SortFilterOptions filterOptions)
        {
            if (!filterOptions.FilteringEnabled)
                return true;

            if (_romExclusions.Contains(game.RomName))
                return false;

            string fullDescription = game.RawParentheticalInfo;

            if (!string.IsNullOrEmpty(fullDescription))
            {
                foreach (var keyword in DisqualifyingKeywords)
                {
                    if (fullDescription.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // Revision gate: parents are always exempt, regardless of their own description text -
            // revision is inherently relative to a parent (a clone's revision compared to it), so a
            // parent can't meaningfully "be a revision of itself" even when MAME's naming convention
            // happens to use that word (e.g. "1942 (Revision B)" as the canonical/parent entry).
            if (!string.IsNullOrEmpty(game.CloneOf) && IsRevisionTagged(fullDescription) && !filterOptions.IncludeRevisions)
                return false;

            // Region gate: empty EnabledRegions means no restriction (checkbox-row semantics - checking
            // nothing behaves like checking everything). "Other" is a real bucket populated by
            // recognized-but-uncommon country words (Czech, Brazil, Argentina, etc. - see
            // RegionSynonyms). A title with zero region information at all (no recognized region word
            // anywhere in its description) falls into its own "Unspecified" bucket instead of always
            // failing the gate - checked independently via its own checkbox, same as Other.
            if (filterOptions.EnabledRegions.Count > 0)
            {
                var detectedRegions = ParseRegions(fullDescription);
                if (detectedRegions.Count == 0)
                {
                    if (!filterOptions.EnabledRegions.Contains("Unspecified"))
                        return false;
                }
                else if (!detectedRegions.Overlaps(filterOptions.EnabledRegions))
                {
                    return false;
                }
            }

            // Player count gate: same unspecified-passes rule - Players == 0 means -listxml didn't report
            // a count, so it isn't held against the ROM.
            if (filterOptions.EnabledPlayerCounts.Count > 0 && game.Players > 0)
            {
                if (!filterOptions.EnabledPlayerCounts.Contains(game.Players))
                    return false;
            }

            // Year gate: unparseable/placeholder years (empty, "19??", "1988?", etc.) pass by default,
            // same fail-open philosophy as every other unspecified-data case here. Only a cleanly numeric
            // Year gets held against the slider range.
            if (int.TryParse(game.Year, out int gameYear))
            {
                if (gameYear < filterOptions.MinimumYear || gameYear > filterOptions.MaximumYear)
                    return false;
            }

            // Manufacturer gate: empty/null SelectedManufacturer means no restriction. Contains-based
            // rather than exact match, so one checkbox-equivalent choice covers naming variants
            // (Atari / Atari Games, Taito / Taito Corporation / Taito Corporation Japan, etc.).
            if (!string.IsNullOrEmpty(filterOptions.SelectedManufacturer))
            {
                if (string.IsNullOrEmpty(game.Manufacturer) ||
                    !game.Manufacturer.Contains(filterOptions.SelectedManufacturer, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        // Parent-level rating gate

        // Parent-level rating gate - the only filter that can exclude a parent outright (and by
        // extension, every one of its clones, which have no independent rating of their own to fall back
        // on). A parent qualifies if its band's floor meets the threshold (e.g. a "70 to 80" band passes
        // a MinimumRating of 70). Unrecognized ROMs pass by default (fail open) rather than being
        // excluded for data sort_database.ini simply doesn't have an entry for.
        // Resolves a game's display rating for the media panel (Column Two's Publisher/Year/Rating row) -
        // separate from the filtering-only IsParentRatingEligible below. sort_database.ini stores a
        // 10-point band per parent ROM (e.g. "70 to 80"), clones inheriting their parent's band with no
        // independent rating of their own - same CloneOf-based parent resolution AreFilterCriteriaMet
        // already uses elsewhere in this file. Display value is just the band's ceiling divided by 10
        // (0-10 band -> "1", 90-100 band -> "10"), per project convention. Returns null (not 0) when the
        // ROM has no sort_database.ini entry at all, so callers can distinguish "no rating" from "rated 1".
        public int? GetDisplayRating(Models.GameItem game)
        {
            if (_cachedParentLookup == null) return null;

            string parentRomName = string.IsNullOrEmpty(game.CloneOf) ? game.RomName : game.CloneOf;

            if (!_cachedParentLookup.TryGetValue(parentRomName, out var entry)) return null;

            return entry.RatingMax / 10;
        }

        public bool IsParentRatingEligible(string romName, SortFilterOptions filterOptions)
        {
            if (!filterOptions.FilteringEnabled)
                return true;

            if (_cachedParentLookup == null || !_cachedParentLookup.TryGetValue(romName, out var entry))
                return true;

            // Range overlap check: the game's band [RatingMin, RatingMax] qualifies if it overlaps at
            // all with the filter's [MinimumRating, MaximumRating] range. Using RatingMax against the
            // floor alone (the old single-slider check) meant the top band ("90 to 100") could never
            // satisfy a MinimumRating of 100 - this overlap form handles both ends correctly.
            return entry.RatingMax >= filterOptions.MinimumRating && entry.RatingMin <= filterOptions.MaximumRating;
        }

        // Parent-level genre gate - same unspecified/empty-set-means-no-restriction semantics as every
        // other checkbox-row filter. Checks only the ROOT category (Category's first "/"-split segment)
        // against the fixed 8-genre checkbox list in the Sorting window - a game's subcategory (e.g.
        // "Shooter / Flying Horizontal") doesn't need independent matching, just its top-level genre.
        // Unrecognized/untracked ROMs pass by default (fail open), matching IsParentRatingEligible.
        public bool IsParentGenreEligible(string romName, SortFilterOptions filterOptions)
        {
            if (!filterOptions.FilteringEnabled)
                return true;

            if (filterOptions.EnabledGenres.Count == 0)
                return true;

            if (_cachedParentLookup == null || !_cachedParentLookup.TryGetValue(romName, out var entry))
                return true;

            string rootGenre = entry.Category
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;

            return filterOptions.EnabledGenres.Contains(rootGenre);
        }

        // Single entry point for tree-building - determines whether a game (parent OR clone) should be
        // shown, combining the rating check (always evaluated against the family's parent, since clones
        // have no independent rating of their own) with the keyword/region/revision/player-count checks
        // in IsVariantEligible. Callers no longer need separate parent/clone code paths - both
        // BuildVirtualTree and the custom-folder branch call this uniformly for every game.
        public bool IsGameEligible(GameItem game, SortFilterOptions filterOptions)
        {
            if (!filterOptions.FilteringEnabled)
                return true;

            string parentRomName = string.IsNullOrEmpty(game.CloneOf) ? game.RomName : game.CloneOf;

            // Untracked gate: a ROM whose family has no sort_database.ini entry at all can't be judged on
            // Rating (no Score) or placed in a category (no Category), and we have no curated opinion on
            // it - once filtering is engaged, it's excluded entirely, same principle as the bootleg/hack/
            // prototype keyword gate. Only reachable here since FilteringEnabled is already confirmed true.
            if (_cachedParentLookup == null || !_cachedParentLookup.ContainsKey(parentRomName))
                return false;

            if (!IsParentRatingEligible(parentRomName, filterOptions))
                return false;

            if (!IsParentGenreEligible(parentRomName, filterOptions))
                return false;

            return IsVariantEligible(game, filterOptions);
        }
        // [END SECTION: Clone/Variant Eligibility Filter]

        // [SECTION: Virtual Tree Builder]
        // Builds the category-based virtual tree from the user's current GamesCollection, using the
        // cached sort_database.ini lookups. ROMs not found as a parent or variant are excluded entirely.
        // Clone/variant ROMs must also pass IsVariantEligible; survivors get a parenthetical
        // differentiator appended to their display name (parent titles stay unmodified). Category
        // strings split on " / " produce nested subfolder levels.
        public ObservableCollection<TreeCategoryNode> BuildVirtualTree(IEnumerable<GameItem> games, Brush folderColor, SortFilterOptions filterOptions)
        {
            var rootCategories = new Dictionary<string, TreeCategoryNode>(StringComparer.OrdinalIgnoreCase);

            if (_cachedParentLookup == null)
            {
                // EnsureSortDatabaseLoadedAsync should be awaited before calling this - return an empty
                // tree rather than throwing, so a missed await fails safe instead of crashing the UI.
                return new ObservableCollection<TreeCategoryNode>(rootCategories.Values);
            }

            // Reverse index: ParentROM -> physically-owned GameItems whose CloneOf points at it. Built
            // live from GamesCollection every call instead of a separately-generated ini list, so it can
            // never drift out of sync with the actual ROM set - it always reflects whatever
            // mame_cache.json most recently reported.
            var cloneChildrenByParent = games
                .Where(g => !string.IsNullOrEmpty(g.CloneOf))
                .GroupBy(g => g.CloneOf, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(grp => grp.Key, grp => grp.ToList(), StringComparer.OrdinalIgnoreCase);

            // All eligibility/dedup logic now lives in ResolveVisibleFamilyMembers - computed once here,
            // shared with the custom-folder branch (MainViewModel), so both structure modes can never
            // disagree on filtering behavior since there's only one implementation of it.
            var visibleRomNames = ResolveVisibleFamilyMembers(games, filterOptions);

            foreach (var game in games)
            {
                string romName = game.RomName.ToLowerInvariant();

                // Only parents (and visible clones added below) reach the virtual tree - ROMs that are
                // themselves a clone of something are skipped here and only ever appear via the
                // clone-inclusion pass beneath their parent.
                if (!_cachedParentLookup.TryGetValue(romName, out var entry))
                    continue;

                bool parentVisible = visibleRomNames.Contains(game.RomName);

                cloneChildrenByParent.TryGetValue(romName, out var clones);
                var visibleClones = clones?.Where(c => visibleRomNames.Contains(c.RomName)).ToList() ?? new List<GameItem>();

                if (!parentVisible && visibleClones.Count == 0)
                    continue;

                var categoryParts = entry.Category
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                if (categoryParts.Length == 0) continue;

                string rootHeader = categoryParts[0].ToUpper();
                if (!rootCategories.TryGetValue(rootHeader, out var currentPointer))
                {
                    currentPointer = new TreeCategoryNode { HeaderText = rootHeader, FolderColor = folderColor };
                    rootCategories[rootHeader] = currentPointer;
                }

                for (int i = 1; i < categoryParts.Length; i++)
                {
                    string subHeader = categoryParts[i].ToUpper();
                    var existingSub = currentPointer.SubFolders.FirstOrDefault(sf => string.Equals(sf.HeaderText, subHeader, StringComparison.OrdinalIgnoreCase));

                    if (existingSub == null)
                    {
                        existingSub = new TreeCategoryNode { HeaderText = subHeader, FolderColor = folderColor };
                        currentPointer.SubFolders.Add(existingSub);
                    }
                    currentPointer = existingSub;
                }

                if (parentVisible)
                {
                    game.DisplayTitle = game.FullTitle;
                    currentPointer.TryAddChildGame(game);
                }

                // Clone inclusion pass: survivors display flat as siblings of their parent in the same
                // folder - nesting parent-as-folder was explicitly rejected since it conflicts with
                // launch-on-double-click leaf node behavior. Runs regardless of whether the parent itself
                // was visible, so a filtered-out parent doesn't take its otherwise-visible clones with it.
                foreach (var variantGame in visibleClones)
                {
                    string differentiator = BuildCloneDifferentiator(variantGame);
                    variantGame.DisplayTitle = string.IsNullOrEmpty(differentiator)
                        ? variantGame.FullTitle
                        : $"{variantGame.FullTitle} {differentiator}".Trim();

                    currentPointer.TryAddChildGame(variantGame);
                }
            }

            // Alphabetical ordering, root and nested, mirrors the existing custom-folder tree's behavior
            var sortedRoots = rootCategories.Values.OrderBy(n => n.HeaderText, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var root in sortedRoots)
            {
                SortSubFoldersRecursively(root);
            }

            return new ObservableCollection<TreeCategoryNode>(sortedRoots);
        }

        private void SortSubFoldersRecursively(TreeCategoryNode node)
        {
            var sorted = node.SubFolders.OrderBy(sf => sf.HeaderText, StringComparer.OrdinalIgnoreCase).ToList();
            node.SubFolders.Clear();
            foreach (var sub in sorted)
            {
                node.SubFolders.Add(sub);
                SortSubFoldersRecursively(sub);
            }
        }
        // [END SECTION: Virtual Tree Builder]
    }
}