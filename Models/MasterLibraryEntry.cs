namespace ArcadeStick.Models
{
    // [SECTION: MasterLibraryEntry Model]
    // Represents one parsed line from sort_database.ini:
    //   ParentROM = Score:DEFAULT | Category:Genre/Subgenre
    // Score is captured in full now even though it's not used for sorting yet (DEFAULT is a placeholder
    // for a future user-ratings system) - this avoids a schema rewrite when that lands. Variants was
    // retired: it was a machine-generated snapshot of MAME's own clone data, now superseded by
    // GameItem.CloneOf (sourced live from mame_cache.json on every regeneration, so it can't drift out
    // of sync the way a separately-generated ini field could).
    public class MasterLibraryEntry
    {
        // The parent ROM's short name (e.g. "1943"), as it appears on the left side of the ini line
        public string RomName { get; set; } = string.Empty;

        // Raw score field from the ini (e.g. "DEFAULT" until user ratings are implemented, at which
        // point saved ratings will override this value). Not used for sorting yet.
        public string Score { get; set; } = string.Empty;

        // Full category string as it appears in the ini (e.g. "Shooter / Flying Vertical").
        // Split on " / " at tree-build time to produce nested subfolder levels.
        public string Category { get; set; } = string.Empty;

        // Rating band this parent falls under, parsed from the ini's bracket section header
        // (e.g. "[70 to 80 (...)]" -> RatingMin=70, RatingMax=80). Clones inherit their parent's band -
        // there's no separate per-clone rating. Defaults to the widest possible band (0-100) if parsing
        // ever fails, so a malformed header fails open rather than silently hiding entries.
        public int RatingMin { get; set; } = 0;
        public int RatingMax { get; set; } = 100;
    }
    // [END SECTION: MasterLibraryEntry Model]
}