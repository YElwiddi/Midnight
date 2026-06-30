/// <summary>
/// The distinct endings of the game. The enum order matches the menu slots (I..V).
/// </summary>
public enum Ending
{
    WrathfulSpirit = 0,    // Bad    - killed by the spirit when sanity reaches 0
    Ambush = 1,            // Bad    - killed by the grave robber
    WrongfulConviction = 2,// Good   - beat the game and exit the crypt
    HappilyEverAfter = 3,  // Good?  - killed by the bride in the church
    Father = 4,            // Secret - caught by the FatherKiller in the forest finale
    Maria = 5              // Bad    - let the hanged woman in; killed by her at the tree
}

/// <summary>
/// Static, designer-editable display data for each ending (title, type label, flavor text).
/// </summary>
public static class EndingInfo
{
    public struct Data
    {
        public string roman;
        public string title;
        public string typeLabel;
        public string description;
        public bool isGood;
    }

    /// <summary>Endings in menu/slot order (I, II, III, IV, V).</summary>
    public static readonly Ending[] InOrder =
    {
        Ending.WrathfulSpirit,
        Ending.Ambush,
        Ending.WrongfulConviction,
        Ending.HappilyEverAfter,
        Ending.Father,
        Ending.Maria
    };

    public static Data Get(Ending e)
    {
        switch (e)
        {
            case Ending.WrathfulSpirit:
                return new Data
                {
                    roman = "I", title = "Bad Ending - A Wrathful Spirit", typeLabel = "Ending", isGood = false,
                    description = "You lost all your sanity. Stay in the light and check your visitors."
                };
            case Ending.Ambush:
                return new Data
                {
                    roman = "II", title = "Bad Ending - An Ambush", typeLabel = "Ending", isGood = false,
                    description = "You didn't secure the Graveyard. Patrol the graveyard and check the cameras more often. Don't be too trusting."
                };
            case Ending.WrongfulConviction:
                return new Data
                {
                    roman = "III", title = "Good Ending - A Wrongful Conviction", typeLabel = "Ending", isGood = true,
                    description = "The Graveyard is at peace."
                };
            case Ending.HappilyEverAfter:
                return new Data
                {
                    roman = "IV", title = "Good Ending? - Happily Ever After", typeLabel = "Ending", isGood = true,
                    description = "Congratulations!"
                };
            case Ending.Father:
                return new Data
                {
                    roman = "V", title = "Hidden Ending - The Father's Fate", typeLabel = "Ending", isGood = false,
                    description = "Did you find what you were looking for?"
                };
            case Ending.Maria:
                return new Data
                {
                    roman = "VI", title = "Bad Ending - The Hanged Woman", typeLabel = "Ending", isGood = false,
                    description = "You let her in. Some guests should never be allowed past the gate."
                };
            default:
                return new Data { roman = "?", title = "??????", typeLabel = "", description = "" };
        }
    }

    /// <summary>
    /// Two-line reveal text shown on the ending screen, e.g.
    /// "Bad Ending #1" (smaller) then "A Wrathful Spirit" on a new line.
    /// </summary>
    public static string RevealText(Ending e)
    {
        Data d = Get(e);
        int number = (int)e + 1;
        return "<size=45%>" + d.typeLabel + " #" + number + "</size>\n" + d.title;
    }
}
