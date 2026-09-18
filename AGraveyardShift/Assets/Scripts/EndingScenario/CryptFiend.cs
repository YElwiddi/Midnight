/// <summary>
/// The three killers ("crypt fiends") that can stalk the player in the crypt.
/// Which one spawns is decided by the player's dominant stat in EndingScenarioManager:
///   scared -> Friend  (PersistentStalker)
///   mean   -> Wraith  (FastChaser)
///   stupid -> Beast   (FlashlightDisabler)
/// Enum order matches the menu slot order (Friend, Wraith, Beast).
/// </summary>
public enum CryptFiend
{
    Friend = 0, // player_scared dominant — PersistentStalker
    Wraith = 1, // player_mean dominant   — FastChaser
    Beast  = 2  // player_stupid dominant — FlashlightDisabler
}

/// <summary>
/// Static, designer-editable display data for each crypt fiend (name + flavor lore).
/// The "spawn condition" lines are in-universe flavor text and are intentionally
/// independent of the actual gameplay spawn rule (the dominant-stat mapping above).
/// </summary>
public static class CryptFiendInfo
{
    public struct Data
    {
        public string title;          // e.g. "The Friend"
        public string flavor;         // one or two sentences
        public string ability;        // the "Unique ability:" value
        public string spawnCondition; // the "Spawn condition:" value
    }

    /// <summary>Fiends in menu/slot order.</summary>
    public static readonly CryptFiend[] InOrder =
    {
        CryptFiend.Friend,
        CryptFiend.Wraith,
        CryptFiend.Beast
    };

    /// <summary>
    /// Display data in the active language. Looked up under "fiend.&lt;id&gt;.title" / ".flavor" /
    /// ".ability" / ".spawn" in the string tables; the English below is the source.
    /// </summary>
    public static Data Get(CryptFiend f)
    {
        Data d = GetEnglish(f);
        string id = Id(f);
        if (string.IsNullOrEmpty(id)) return d;
        d.title = Loc.Get("fiend." + id + ".title", d.title);
        d.flavor = Loc.Get("fiend." + id + ".flavor", d.flavor);
        d.ability = Loc.Get("fiend." + id + ".ability", d.ability);
        d.spawnCondition = Loc.Get("fiend." + id + ".spawn", d.spawnCondition);
        return d;
    }

    /// <summary>Stable id used to build the string-table keys.</summary>
    public static string Id(CryptFiend f)
    {
        switch (f)
        {
            case CryptFiend.Friend: return "friend";
            case CryptFiend.Wraith: return "wraith";
            case CryptFiend.Beast: return "beast";
            default: return null;
        }
    }

    /// <summary>The authored (English) data — the source text for translators.</summary>
    public static Data GetEnglish(CryptFiend f)
    {
        switch (f)
        {
            case CryptFiend.Friend:
                return new Data
                {
                    title = "The Friend",
                    flavor = "Slow, but methodical. Adept at persistence.",
                    ability = "Flashlight sensitivity.",
                    spawnCondition = "Signs of low intelligence, feeble-mindedness."
                };
            case CryptFiend.Wraith:
                return new Data
                {
                    title = "The Wraith",
                    flavor = "Fast, aggressive, deadly.",
                    ability = "Fatal hunting speed.",
                    spawnCondition = "Signs of overconfidence, arrogance, bravery."
                };
            case CryptFiend.Beast:
                return new Data
                {
                    title = "The Beast",
                    flavor = "Silent, and elusive. Doesn't like to show itself.",
                    ability = "Imperception & electrical interference.",
                    spawnCondition = "Signs of timidness, shyness, or weakness."
                };
            default:
                return new Data { title = "???", flavor = "", ability = "", spawnCondition = "" };
        }
    }

    /// <summary>
    /// Rich-text body shown in the fiend detail view: flavor line, then the
    /// labelled ability and spawn-condition lines.
    /// </summary>
    public static string Body(CryptFiend f)
    {
        Data d = Get(f);
        return d.flavor +
               "\n\n<b>" + Loc.Get("fiend.label.ability", "Unique ability:") + "</b>  " + d.ability +
               "\n<b>" + Loc.Get("fiend.label.spawn", "Spawn condition:") + "</b>  " + d.spawnCondition;
    }

    /// <summary>
    /// Maps EndingScenarioManager's dominant-stat string ("mean"/"scared"/"stupid")
    /// to the fiend that was encountered. Returns false for unknown/empty input.
    /// </summary>
    public static bool TryFromDominantStat(string dominantStat, out CryptFiend fiend)
    {
        switch (dominantStat)
        {
            case "scared": fiend = CryptFiend.Friend; return true;
            case "mean":   fiend = CryptFiend.Wraith; return true;
            case "stupid": fiend = CryptFiend.Beast;  return true;
            default:       fiend = CryptFiend.Friend; return false;
        }
    }
}
