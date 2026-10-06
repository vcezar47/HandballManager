using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// Turns what happened on the court into a line of text — the feed beside the 2D view, and the
/// whole of the classic text view. Because the engine knows where every shot came from, the
/// commentary can say so: a goal from the left wing reads like one.
/// </summary>
public static class Commentary
{
    /// <summary>The event as the rest of the game files it, or null for the ones too routine to list.</summary>
    public static LiveMatchEvent? ToLiveEvent(PhysicsMatch m, MatchEvent2D e, Random rng)
    {
        string? type = e.Kind switch
        {
            EventKind.Goal or EventKind.SevenMetreGoal => "Goal",
            EventKind.Save or EventKind.SevenMetreSaved => "Save",
            EventKind.Miss or EventKind.Post or EventKind.SevenMetreMissed => "Miss",
            EventKind.Turnover or EventKind.Interception => "Turnover",
            EventKind.TwoMinutes => "Suspension",
            EventKind.RedCard => "RedCard",
            EventKind.Substitution => "Substitution",
            EventKind.Timeout => "Timeout",
            EventKind.Injury => "Injury",
            EventKind.KempaAttempt or EventKind.KempaFailed or EventKind.EmptyGoal or EventKind.OffensiveFoul => "Status",
            EventKind.PassivePlay => "PassivePlay",
            EventKind.Block or EventKind.SevenMetreAwarded or EventKind.PassiveWarning
                or EventKind.SuspensionOver or EventKind.HalfTime or EventKind.FullTime
                or EventKind.ExtraTime or EventKind.ShootoutGoal or EventKind.ShootoutMiss => "Status",
            _ => null,
        };
        if (type == null) return null;

        int teamId = e.Side >= 0 ? m.Sides[e.Side].Team.Id : 0;
        return new LiveMatchEvent
        {
            Minute = e.Minute,
            Second = e.Second,
            EventType = type,
            TeamId = teamId,
            PlayerId = e.PlayerId == 0 ? null : e.PlayerId,
            PlayerName = e.PlayerName,
            AssistPlayerId = e.Kind == EventKind.Goal && e.OtherPlayerId != 0 ? e.OtherPlayerId : null,
            AssistPlayerName = e.Kind == EventKind.Goal ? e.OtherPlayerName : null,
            Description = Describe(m, e, rng),
        };
    }

    public static string Describe(PhysicsMatch m, MatchEvent2D e, Random rng)
    {
        string team = e.Side >= 0 ? m.Sides[e.Side].Team.Name : "";
        string who = e.PlayerName;
        string other = e.OtherPlayerName;
        var shot = m.Shots.LastOrDefault(s => s.PlayerId == e.PlayerId && s.Time <= e.Time + 0.01f && s.Time > e.Time - 3f);

        string Pick(params string[] lines) => lines[rng.Next(lines.Length)];

        return e.Kind switch
        {
            EventKind.Goal => GoalLine(who, other, shot, rng),
            EventKind.SevenMetreGoal => Pick($"GOAL! {who} converts the seven-metre.", $"GOAL! {who} makes no mistake from the line.",
                                             $"GOAL! {who} sends {other} the wrong way from seven metres."),
            EventKind.SevenMetreSaved => Pick($"SAVED! {who} reads the seven-metre and keeps it out.", $"{who} guesses right and denies {other} from the line!"),
            EventKind.SevenMetreMissed => $"{who} misses from seven metres!",
            EventKind.SevenMetreAwarded => Pick($"Seven-metre throw to {team}! {who} steps up.", $"The referees point to the line — seven metres for {team}."),
            EventKind.Save => SaveLine(who, other, shot, rng),
            EventKind.Miss => Pick($"{who} shoots wide{Where(shot)}.", $"Over the bar from {who}{Where(shot)}.", $"{who} pulls it wide of the post."),
            EventKind.Post => Pick($"OFF THE POST! {who} is denied by the woodwork.", $"{who} rattles the frame{Where(shot)}!"),
            EventKind.Block => Pick($"BLOCKED! {who} gets a hand to {other}'s shot.", $"{who} rises in the wall and blocks {other}."),
            EventKind.Interception => Pick($"{who} reads the pass and steals it from {other}!", $"Intercepted! {who} cuts out the ball meant for a {team} opponent."),
            EventKind.Turnover => Pick($"{who} wins the ball for {team}.", $"Turnover — {team} have it back through {who}."),
            EventKind.TwoMinutes => Pick($"TWO MINUTES! {who} ({team}) is sent to the bench.", $"{who} is suspended for two minutes — {team} a player short."),
            EventKind.RedCard => $"RED CARD! A third suspension for {who} — she is disqualified.",
            EventKind.SuspensionOver => $"{team} are back to full strength.",
            EventKind.PassiveWarning => $"The referee's arm goes up — passive play warning to {team}.",
            EventKind.PassivePlay => $"PASSIVE PLAY! {team} run out of passes and lose the ball.",
            EventKind.Substitution => $"Substitution {team}: {who} on for {other}.",
            EventKind.Timeout => $"TIME-OUT {team}. The bench gathers the players.",
            EventKind.FreeThrow => $"Free throw {team} — {other} stops {who}.",
            EventKind.OffensiveFoul => $"Offensive foul by {other} — free throw {team}.",
            EventKind.EmptyGoal => $"{team} take their keeper off — seven court players, and an empty goal behind them.",
            EventKind.KempaAttempt => $"{who} lobs it into the area for {other}…",
            EventKind.KempaFailed => $"{who} goes up for the Kempa, but can't hold it — the keeper's ball.",
            EventKind.Injury => $"{who} is down and signalling to the bench — that looks like a {m.LastInjuryName.ToLowerInvariant()}. The referees stop play.",
            EventKind.ExtraTime => $"Still level at {m.Score[0]}–{m.Score[1]} — extra time!",
            EventKind.ShootoutGoal => $"Shoot-out: {who} scores for {team}. ({m.ShootoutScore[0]}–{m.ShootoutScore[1]} so far)",
            EventKind.ShootoutMiss => $"Shoot-out: {who} is denied by {other}!",
            EventKind.HalfTime => $"HALF-TIME: {m.Sides[0].Team.Name} {m.Score[0]}–{m.Score[1]} {m.Sides[1].Team.Name}.",
            EventKind.FullTime => $"FULL-TIME: {m.Sides[0].Team.Name} {m.Score[0]}–{m.Score[1]} {m.Sides[1].Team.Name}.",
            _ => "",
        };
    }

    private static string GoalLine(string who, string assist, ShotRecord? shot, Random rng)
    {
        if (shot?.Kempa == true)
            return $"KEMPA! {assist} lobs it in and {who} finishes in mid-air! What a goal!";
        string from = shot == null ? "" : shot.Role switch
        {
            "LW" => rng.Next(2) == 0 ? " dives in from the left wing and" : " takes off from the left corner and",
            "RW" => rng.Next(2) == 0 ? " dives in from the right wing and" : " takes off from the right corner and",
            "Pivot" => rng.Next(2) == 0 ? " spins at the line and" : " turns her defender at six metres and",
            _ when shot.Distance >= 8.5f => rng.Next(2) == 0 ? " rises at nine metres and" : " unleashes from distance and",
            _ => rng.Next(2) == 0 ? " breaks through the middle and" : " bursts past her marker and",
        };
        string finish = rng.Next(3) switch { 0 => "finds the corner", 1 => "beats the keeper", _ => "buries it" };
        string pass = string.IsNullOrEmpty(assist) ? "" : $" Set up by {assist}.";
        return $"GOAL! {who}{from} {finish}.{pass}";
    }

    private static string SaveLine(string keeper, string shooter, ShotRecord? shot, Random rng)
    {
        string what = shot == null ? "the shot" : shot.Distance >= 8.5f ? "the long-range effort" : shot.Role is "LW" or "RW" ? "the wing shot" : "the close-range shot";
        return rng.Next(3) switch
        {
            0 => $"SAVE! {keeper} stops {what} from {shooter}.",
            1 => $"{keeper} gets across to deny {shooter}!",
            _ => $"Great save by {keeper} — {shooter} can't believe it.",
        };
    }

    private static string Where(ShotRecord? shot) => shot == null ? "" : shot.Role switch
    {
        "LW" => " from the left wing",
        "RW" => " from the right wing",
        "Pivot" => " from the line",
        _ when shot.Distance >= 8.5f => " from distance",
        _ => "",
    };
}
