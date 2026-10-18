using HandballManager.Localization;
using HandballManager.Services;

namespace HandballManager.Engine2D;

/// <summary>The four shapes a scoreboard comes in. Every one is a single row, so it sits in the middle of the top bar.</summary>
public enum ScoreboardShape
{
    /// <summary>The EHF broadcast board: one dark bar with the crest between the scores, the clock in a tab hung beneath it.</summary>
    Broadcast,

    /// <summary>Blocks side by side: the crest, each team's code, the two scores in accent boxes, the clock.</summary>
    Tabs,

    /// <summary>A light ribbon with a coloured crest block at the left and the clock in a dark block at the right.</summary>
    Ribbon,

    /// <summary>Slanted blocks: crest, teams and scores, clock.</summary>
    Angular,
}

/// <summary>Which kind of competition a match belongs to, for the tag a cup or supercup board carries.</summary>
public enum CompetitionKind { League, Cup, Supercup, European }

/// <summary>
/// How a match's scoreboard looks, in the style of the competition's own television graphics.
/// </summary>
/// <param name="Primary">The competition's colour: the crest and clock blocks (the clock tab on a broadcast board).</param>
/// <param name="Accent">The score boxes, and the tag a cup or supercup board carries.</param>
/// <param name="Bar">The dark ground the team codes sit on. Ignored by the ribbon, which is light.</param>
/// <param name="Tag">"SEMI-FINAL", "SUPERCUP" and the like, or null for a league match.</param>
/// <param name="Gold">A supercup: the board is trimmed in gold.</param>
/// <param name="ShowsHalf">Whether the clock says which half it is. The Champions League's board shows the time alone.</param>
public sealed record ScoreboardStyle(
    ScoreboardShape Shape,
    string Primary, string OnPrimary,
    string Accent, string OnAccent,
    string Bar,
    string? Tag = null,
    bool Gold = false,
    bool ShowsHalf = true)
{
    public const string GoldColour = "#D4AF37";
    public const string OnGold = "#2A2108";

    /// <summary>The red of a two-minute timer, the same on every board.</summary>
    public const string SuspensionColour = "#D3202A";

    /// <summary>
    /// The board for a competition. Each league has its own; a cup borrows its league's shape in
    /// the cup's colours; a supercup is the league's board trimmed in gold.
    /// </summary>
    /// <param name="competition">The competition key the fixture is filed under.</param>
    /// <param name="round">A cup fixture's stored round ("SemiFinal"), for the tag; null for a league match.</param>
    public static ScoreboardStyle For(string competition, CompetitionKind kind, string? round = null)
    {
        if (kind == CompetitionKind.European || European.ContainsKey(competition))
            return European.GetValueOrDefault(competition, European[LeagueService.ChampionsLeagueCompetition]);

        var league = Leagues.GetValueOrDefault(competition, Fallback);

        return kind switch
        {
            CompetitionKind.Cup => (Cups.TryGetValue(competition, out var cup) ? cup(league) : league) with { Tag = RoundTag(round) },
            CompetitionKind.Supercup => league with
            {
                Accent = GoldColour, OnAccent = OnGold, Gold = true,
                // Only a final-four supercup has rounds to name; a single match keeps the model's
                // default round, which would otherwise read as a semi-final.
                Tag = round is "SemiFinal" or "ThirdPlace" && Helpers.CompetitionAssets.SupercupIsFinalFour(competition)
                    ? $"{Loc.T("SUPERCUP")} · {RoundTag(round)}"
                    : Loc.T("SUPERCUP"),
            },
            _ => league,
        };
    }

    /// <summary>A cup's stored round as the short label a board can carry.</summary>
    public static string? RoundTag(string? round) => round switch
    {
        "Final" => Loc.T("FINAL"),
        "SemiFinal" => Loc.T("SEMI-FINAL"),
        "QuarterFinal" => Loc.T("QUARTER-FINAL"),
        "ThirdPlace" => Loc.T("3RD PLACE"),
        "Round16" or "Last16" => Loc.T("LAST 16"),
        _ => null,
    };

    /// <summary>
    /// "1st", "2nd", "ET" — the half beside the clock; at the end "FT", "AET" after extra time, or
    /// the shoot-out's score when it took one.
    /// </summary>
    public static string HalfLabel(int period, bool halfTime, bool fullTime,
        bool shootout = false, int homeShootout = 0, int awayShootout = 0) =>
        fullTime ? (shootout ? Loc.F("PENS {0}–{1}", homeShootout, awayShootout) : period > 2 ? Loc.T("AET") : Loc.T("FT"))
        : halfTime ? Loc.T("HT")
        : period switch { 1 => Loc.T("1st"), 2 => Loc.T("2nd"), _ => Loc.T("ET") };

    // ─── The boards ─────────────────────────────────────────────────────────────
    //
    // Colours from each competition's crest and broadcast identity. These are the game's own
    // designs in each competition's colours, not copies of any broadcaster's graphics — except
    // the three EHF boards, which follow the EHF's own on-screen board.

    private static readonly ScoreboardStyle Fallback = new(ScoreboardShape.Tabs, "#22304F", "#FFFFFF", "#E94560", "#FFFFFF", "#141B30");

    private static readonly Dictionary<string, ScoreboardStyle> European = new()
    {
        [LeagueService.ChampionsLeagueCompetition] = new(ScoreboardShape.Broadcast, "#C9A84C", "#0C1733", "#DADADA", "#0C1733", "#0C1733", ShowsHalf: false),
        [LeagueService.EuropeanLeagueCompetition] = new(ScoreboardShape.Broadcast, "#E8501E", "#FFFFFF", "#DADADA", "#111A3A", "#111A3A"),
        [LeagueService.EuropeanCupCompetition] = new(ScoreboardShape.Broadcast, "#24418F", "#FFFFFF", "#DADADA", "#0D1430", "#0D1430"),
    };

    private static ScoreboardStyle B(string p, string onP, string a, string onA, string bar) => new(ScoreboardShape.Tabs, p, onP, a, onA, bar);
    private static ScoreboardStyle R(string p, string a, string onA) => new(ScoreboardShape.Ribbon, p, "#FFFFFF", a, onA, "#F4F5F7");
    private static ScoreboardStyle A(string p, string onP, string a, string onA, string bar) => new(ScoreboardShape.Broadcast, p, onP, a, onA, bar);
    private static ScoreboardStyle E(string p, string a, string onA, string bar) => new(ScoreboardShape.Angular, p, "#FFFFFF", a, onA, bar);

    private static readonly Dictionary<string, ScoreboardStyle> Leagues = new()
    {
        [LeagueService.LigaFlorilorCompetition] = B("#3F668C", "#FFFFFF", "#FEB304", "#14233A", "#12203A"),
        [LeagueService.NbiCompetition] = R("#0086C3", "#006590", "#FFFFFF"),
        [LeagueService.LigueButagazCompetition] = A("#C29C16", "#111111", "#F2F2F2", "#111111", "#111111"),
        [LeagueService.KvindeligaenCompetition] = E("#C8102E", "#FFFFFF", "#8E0B20", "#1A1A1A"),
        [LeagueService.RemaCompetition] = B("#012C8C", "#FFFFFF", "#D61F2E", "#FFFFFF", "#0B1530"),
        [LeagueService.BundesligaCompetition] = B("#8C1D7D", "#FFFFFF", "#F2F2F2", "#3D0B36", "#1F1F24"),
        [LeagueService.SrlCompetition] = R("#0B77B5", "#75C046", "#0E2A10"),
        [LeagueService.HrlCompetition] = E("#0843F4", "#FFFFFF", "#0A2B9C", "#14172B"),
        [LeagueService.PrvaLigaZCompetition] = A("#C72629", "#FFFFFF", "#C2A047", "#2A1F06", "#1A0F10"),
        [LeagueService.HandbollsliganCompetition] = B("#0C2D8C", "#FFFFFF", "#FFD301", "#0C2D8C", "#0E1631"),
        [LeagueService.KslCompetition] = E("#FA4D09", "#FFFFFF", "#9B2C00", "#1C1C1C"),
        [LeagueService.SuperligaCompetition] = R("#E60B14", "#2E8F85", "#FFFFFF"),
        [LeagueService.MolLigaCompetition] = B("#E4002B", "#FFFFFF", "#00843D", "#FFFFFF", "#141414"),
        [LeagueService.LigaGuerrerasCompetition] = A("#00A443", "#FFFFFF", "#C8102E", "#FFFFFF", "#0F1E16"),
        [LeagueService.SplCompetition] = R("#D90011", "#1A1A1A", "#FFFFFF"),
        [LeagueService.ArkusCompetition] = B("#0C4076", "#FFFFFF", "#FFFFFF", "#0C4076", "#0E1A2E"),
        [LeagueService.WhaCompetition] = E("#ED2939", "#FFFFFF", "#9E1622", "#1A1A1A"),
        [LeagueService.MkdCompetition] = B("#D20000", "#FFFFFF", "#FFE600", "#6B0000", "#1A0D0D"),
        [LeagueService.PtCompetition] = A("#046A38", "#FFFFFF", "#DA291C", "#FFFFFF", "#0B1C13"),
        [LeagueService.GrCompetition] = R("#0D5EAF", "#0A2C55", "#FFFFFF"),
        [LeagueService.NlCompetition] = B("#F36C21", "#FFFFFF", "#FFFFFF", "#13203F", "#13203F"),
        [LeagueService.IslCompetition] = B("#02529C", "#FFFFFF", "#DC1E35", "#FFFFFF", "#0C1626"),
    };

    /// <summary>
    /// Each cup in its own colours on its league's shape. Keyed as cup fixtures are filed: by the
    /// league, except the joint MOL Liga's two national cups, which have keys of their own.
    /// </summary>
    private static readonly Dictionary<string, Func<ScoreboardStyle, ScoreboardStyle>> Cups = new()
    {
        [LeagueService.LigaFlorilorCompetition] = l => l with { Primary = "#3E5A8C", Accent = "#DEC555", OnAccent = "#14233A" },
        [LeagueService.NbiCompetition] = l => l with { Primary = "#C9231F", Accent = "#5E8A16", OnAccent = "#FFFFFF" },
        [LeagueService.LigueButagazCompetition] = l => l with { Primary = "#E53323", OnPrimary = "#FFFFFF", Bar = "#00305E", Accent = "#FFFFFF", OnAccent = "#00305E" },
        [LeagueService.KvindeligaenCompetition] = l => l with { Primary = "#8E0B20", Accent = "#E4B23C", OnAccent = "#3A0510" },
        [LeagueService.RemaCompetition] = l => l with { Primary = "#2A5D86", Accent = "#89C8FA", OnAccent = "#0E2A40" },
        [LeagueService.BundesligaCompetition] = l => l with { Primary = "#6A1560" },
        [LeagueService.SrlCompetition] = l => l with { Primary = "#1A96D0", Accent = "#0B4F78", OnAccent = "#FFFFFF" },
        [LeagueService.HrlCompetition] = l => l with { Primary = "#D10A0A", OnAccent = "#9C0505" },
        [LeagueService.PrvaLigaZCompetition] = l => l with { Primary = "#C2A047", OnPrimary = "#1A0F10", Accent = "#C72629", OnAccent = "#FFFFFF" },
        [LeagueService.HandbollsliganCompetition] = l => l with { Primary = "#09509E" },
        [LeagueService.KslCompetition] = l => l with { Primary = "#E30A17", OnAccent = "#8D0008" },
        [LeagueService.SuperligaCompetition] = l => l with { Primary = "#B5001F", Accent = "#3A3A3A" },
        [LeagueService.CzechChampionshipCompetition] = l => l with { Primary = "#11457E", Accent = "#D7141A", OnAccent = "#FFFFFF" },
        [LeagueService.SlovakChampionshipCompetition] = l => l with { Primary = "#0B4EA2", Accent = "#EE1C25", OnAccent = "#FFFFFF" },
        [LeagueService.LigaGuerrerasCompetition] = l => l with { Primary = "#C8102E", Accent = "#F1BF00", OnAccent = "#3D2E00" },
        [LeagueService.SplCompetition] = l => l with { Primary = "#B00012", Accent = "#5A0A0A" },
        [LeagueService.ArkusCompetition] = l => l with { Primary = "#C6363C" },
        [LeagueService.WhaCompetition] = l => l with { Primary = "#B5121F" },
        [LeagueService.MkdCompetition] = l => l with { Primary = "#8A0000" },
        [LeagueService.PtCompetition] = l => l with { Primary = "#DA291C", Accent = "#FFCC29", OnAccent = "#3D2E00" },
        [LeagueService.GrCompetition] = l => l with { Primary = "#1F7FD6", Accent = "#0D5EAF" },
        [LeagueService.NlCompetition] = l => l with { Primary = "#C9531A" },
        [LeagueService.IslCompetition] = l => l with { Primary = "#0072CE", Accent = "#FFFFFF", OnAccent = "#0072CE" },
    };

    /// <summary>The competitions with a board of their own, for the tests.</summary>
    public static IReadOnlyCollection<string> LeagueKeys => Leagues.Keys;

    /// <summary>The cups with colours of their own, for the tests.</summary>
    public static IReadOnlyCollection<string> CupKeys => Cups.Keys;
}
