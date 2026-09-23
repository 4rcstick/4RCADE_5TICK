namespace ArcadeStick.Models
{
    // [SECTION: HistoryEntry Model]
    // One parsed entry from history.xml, already split into the sections the media panel rework needs.
    // GameInfo is the free-text intro before the first recognized "- SECTION -" header (Column Two's
    // Game Info state); Trivia/TipsAndTricks feed Column Two's other two states; Staff/Technical feed
    // Column One Row 3's Staff/Technical states. Sections the entry didn't have are left as empty string,
    // not null, so bound TextBlocks never throw - callers/XAML should treat an empty string as "no data
    // for this section" and show an unavailable placeholder instead.
    public class HistoryEntry
    {
        public string GameInfo { get; set; } = string.Empty;
        public string Trivia { get; set; } = string.Empty;
        public string TipsAndTricks { get; set; } = string.Empty;
        public string Staff { get; set; } = string.Empty;
        public string Technical { get; set; } = string.Empty;

        // Stage 2 groundwork (not yet acted on this pass) - true when GameInfo contains the "please see
        // the original... entry" stub pattern, meaning this entry's real Staff/Technical/Trivia content
        // likely lives under a different (usually parent-region) romname instead. Flagged now since it's
        // free to compute during parsing, even though nothing consumes it yet.
        public bool IsStub { get; set; } = false;
    }
    // [END SECTION: HistoryEntry Model]
}