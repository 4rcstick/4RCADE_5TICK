using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // [SECTION: WikiEntry Model]
    // One hand-curated entry from gameinfo.xml's <display> block - the cleaned-up, launcher-voiced
    // rewrite of the raw Wikipedia extract (which still lives in the same <entry> as an untouched
    // archive under the legacy <intro>/<sections> tags, ignored by this parser). Intro is the headerless
    // opening paragraph; Sections is zero or more themed header+body blocks (e.g. "Gameplay",
    // "Characters") in source order.
    public class WikiEntry
    {
        public string Intro { get; set; } = string.Empty;
        public List<GameInfoBlock> Sections { get; set; } = new();
    }

    // One header+body pair for Column Two's Game Info body - Header is empty for the headerless intro
    // block, populated for every wiki <section header="..."> block. Also reused by MainViewModel for
    // the plain-text history.xml fallback (as a single Header="" block), so XAML only ever needs one
    // ItemsControl/DataTemplate regardless of which source populated it.
    public class GameInfoBlock
    {
        public string Header { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
    // [END SECTION: WikiEntry Model]

    // [SECTION: Wiki Game Info Service]
    // Loads gameinfo.xml (produced by the project's wiki-scrape.ps1 tool, which pulls Wikipedia's
    // plain-text article extracts for a hand-curated romname -> article-title list) into a romname ->
    // WikiEntry lookup. Feeds Column Two's Game Info state as a richer alternative to history.xml's often
    // sparse GameInfo text, when a curated entry exists for that romname - see RefreshHistoryPanelsForSelectedGame
    // in MainViewModel for how this is preferred over HistoryXmlService's GameInfo when both exist.
    //
    // Unlike HistoryXmlService, there's no stub/override resolution here - every entry in this file was
    // hand-picked and hand-verified against the correct Wikipedia article, so whatever's in the file is
    // trusted as-is.
    public class GameInfoService
    {
        private readonly ConfigurationSettings _settings;
        private Dictionary<string, WikiEntry>? _cachedEntries;

        public GameInfoService(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // [SECTION: Load]
        public async Task EnsureWikiGameInfoLoadedAsync()
        {
            if (_cachedEntries != null) return;

            await Task.Run(() =>
            {
                string databaseDir = Path.Combine(_settings.GetArcadeStickFilesPath(), _settings.ConfigSubFolder, "database");
                string wikiPath = Path.Combine(databaseDir, "gameinfo.xml");

                var entries = new Dictionary<string, WikiEntry>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(wikiPath))
                {
                    ParseWikiFile(wikiPath, entries);
                }

                _cachedEntries = entries;
            });
        }

        // Loads the whole file via XmlDocument rather than streaming - gameinfo.xml is expected to
        // stay small (a hand-curated subset of the romset, not the full ~18,550-entry history.xml scale),
        // so the simpler DOM approach is fine here unlike HistoryXmlService's XmlReader streaming.
        private void ParseWikiFile(string path, Dictionary<string, WikiEntry> entries)
        {
            var doc = new XmlDocument();
            try
            {
                doc.Load(path);
            }
            catch
            {
                return; // Malformed file - fail open with an empty dictionary, same convention as elsewhere
            }

            var entryNodes = doc.SelectNodes("/gameEntries/entry");
            if (entryNodes == null) return;

            foreach (XmlNode entryNode in entryNodes)
            {
                string? romName = entryNode.Attributes?["romname"]?.Value;
                if (string.IsNullOrEmpty(romName)) continue;

                // Reads from <display> only - the legacy <intro>/<sections> tags sitting alongside it in
                // the same <entry> are the untouched raw Wikipedia archive, kept in the file for reference
                // but intentionally never parsed here. An entry not yet reworked into <display> (none
                // currently, but a safe default for future additions) is simply skipped rather than
                // falling back to the raw text, since raw Wikipedia extracts were never meant for display.
                XmlNode? displayNode = entryNode.SelectSingleNode("display");
                if (displayNode == null) continue;

                string intro = displayNode.SelectSingleNode("intro")?.InnerText ?? string.Empty;

                var sections = new List<GameInfoBlock>();
                XmlNodeList? sectionNodes = displayNode.SelectNodes("section");
                if (sectionNodes != null)
                {
                    foreach (XmlNode sectionNode in sectionNodes)
                    {
                        string header = sectionNode.Attributes?["header"]?.Value ?? string.Empty;
                        string body = sectionNode.InnerText ?? string.Empty;
                        sections.Add(new GameInfoBlock
                        {
                            Header = header.Trim(),
                            Body = NormalizeParagraphSpacing(body.Trim())
                        });
                    }
                }

                entries[romName] = new WikiEntry
                {
                    Intro = NormalizeParagraphSpacing(intro.Trim()),
                    Sections = sections
                };
            }
        }

        // Wikipedia's plain-text extracts are inconsistent about paragraph spacing - some breaks are a
        // bare single "\n", others (mainly right before a "== Header ==" line) already come with 2-3
        // consecutive newlines. A blanket "double every newline" fix would stack onto the runs that are
        // already spaced, widening the gap before headers instead of just fixing the bare ones. Two-pass
        // fix instead: first collapse any run of 2+ newlines down to exactly one blank line (normalizes
        // the already-spaced breaks, including before headers), then promote any remaining lone single
        // newline (a true paragraph break with no blank line at all) up to a blank line too. End result:
        // every paragraph/header transition gets exactly one consistent blank line, never doubled.
        private static string NormalizeParagraphSpacing(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Some Wikipedia extracts have "blank" lines that actually contain a stray space character
            // rather than being truly empty - left as-is, the collapse/promote passes below can't tell
            // those apart from real single-newline paragraph breaks, causing a double-wide gap. Strip
            // whitespace-only lines down to genuinely empty first so they're treated as ordinary blank
            // lines by the rest of this method.
            string strippedBlankLines = Regex.Replace(text, @"(?m)^[ \t]+$", "");

            string collapsed = Regex.Replace(strippedBlankLines, @"\n{2,}", "\n\n");
            string promoted = Regex.Replace(collapsed, @"(?<!\n)\n(?!\n)", "\n\n");
            return promoted;
        }
        // [END SECTION: Load]

        // [SECTION: Public Lookup]
        public WikiEntry? GetWikiEntry(string romName)
        {
            if (_cachedEntries == null || string.IsNullOrEmpty(romName)) return null;
            return _cachedEntries.TryGetValue(romName, out var entry) ? entry : null;
        }
        // [END SECTION: Public Lookup]
    }
    // [END SECTION: Wiki Game Info Service]
}