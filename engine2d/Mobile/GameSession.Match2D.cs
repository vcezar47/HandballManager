using HandballManager.Engine2D;
using HandballManager.Mobile.Infrastructure;
using HandballManager.Mobile.Match2D;
using HandballManager.Services;

namespace HandballManager.Mobile.Session;

/// <summary>
/// The 2D engine's way into a watched match. Compiled only with -p:Engine2D=true: a store build
/// has no path here and keeps the classic live engine.
/// </summary>
public sealed partial class GameSession
{
    public Match2DViewModel? Match2DVM { get; private set; }

    /// <summary>
    /// Takes the match the squad screen has just set up — both line-ups, the tactics, the crowd,
    /// the knockout flag — and plays it on the 2D court instead.
    /// </summary>
    private async Task<bool> OpenMatch2DAsync(LiveMatchEngine engine)
    {
        MatchPresentation? look = null;
        await RunExclusiveAsync(async () =>
            look = await MatchPresentation.ResolveAsync(Db, engine.HomeTeam, engine.AwayTeam, Clock.CurrentDate));
        if (look == null) return false;

        int userSide = engine.HomeTeam.IsPlayerTeam ? 0 : 1;
        var match = new PhysicsMatch(SetupFor(engine), seed: Environment.TickCount);
        Match2DVM = new Match2DViewModel(match, look, userSide, OnWatchedMatchEnded, new DispatcherFrameTicker(),
            audio: MatchAudio.Shared, ads: InterstitialAds.Shared, preferences: MatchPreferences.Shared);

        var nav = Shell.Current.Navigation;
        var squadPage = nav.NavigationStack.LastOrDefault() as SquadSelectionPage;
        await nav.PushAsync(new Match2DPage(Match2DVM));
        if (squadPage != null) nav.RemovePage(squadPage);
        return true;
    }

    /// <summary>
    /// INSTANT SIM from the squad screen: the same match, played to the end on the 2D engine
    /// with nothing on screen, and filed like a watched one. It used to open the 2D court
    /// instead, because the squad screen had already run the classic engine to full time and
    /// the arena route took any engine it was handed as a match to start.
    /// </summary>
    private async Task InstantMatch2DAsync(LiveMatchEngine engine)
    {
        // A second or two of simulation with the squad screen still up: a second tap must
        // not play the match twice.
        if (_instantSimming) return;
        _instantSimming = true;
        try
        {
            var match = new PhysicsMatch(SetupFor(engine), seed: Environment.TickCount);
            await Task.Run(match.RunToEnd);
            OnWatchedMatchEnded(match);
        }
        finally
        {
            _instantSimming = false;
        }
    }

    private bool _instantSimming;

    /// <summary>Both line-ups, the tactics, the crowd and the knockout flag, as the squad screen set them.</summary>
    private static MatchSetup SetupFor(LiveMatchEngine engine) => new(
        engine.HomeTeam, engine.AwayTeam,
        engine.HomeSquad, engine.AwaySquad,
        // Kick-off instructions, not the live ones: INSTANT SIM hands over an engine that has
        // already played to full time, by when the AI side may have pulled its keeper.
        engine.KickOffHomeTactics.Clone(), engine.KickOffAwayTactics.Clone(),
        engine.HomeAdvantage, engine.IsKnockout,
        VenueName: engine.VenueName, Attendance: engine.Attendance,
        UserSide: engine.HomeTeam.IsPlayerTeam ? 0 : 1);
}
