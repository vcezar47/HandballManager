using System.Diagnostics;
using System.Text.Json;
using HandballManager.Data;
using HandballManager.Engine2D;
using HandballManager.Models;
using HandballManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace HandballManager.Tests;

/// <summary>
/// The 2D engine, headless. Real clubs from the seed files, no database: the engine needs only
/// two teams and two line-ups.
/// </summary>
/// <remarks>
/// The calibration report is inert unless HM_ENGINE2D_CAL=1, because it plays hundreds of
/// matches. It is how the engine is held to the numbers the quick sim is pinned to (see the
/// calibration anchors: 62.9 goals, 3.7 two-minute suspensions and 4.5 seven-metre throws per
/// team, three in four of them scored).
/// </remarks>
[Collection(nameof(SeasonSimulation))]
public class Engine2DTests(ITestOutputHelper output)
{
    private static readonly Lazy<List<Team>> AllClubs = new(LoadClubs);

    private static List<Team> LoadClubs()
    {
        Player.GlobalGameDate = new DateTime(2025, 9, 1);
        var asm = typeof(DatabaseSeeder).Assembly;
        var clubs = new List<Team>();
        int id = 1;
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.Contains("InitialData") && n.EndsWith(".json")))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            var team = JsonSerializer.Deserialize<Team>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (team == null || team.Players.Count < 9) continue;
            team.Id = id++;
            int pid = team.Id * 1000;
            foreach (var p in team.Players) { p.Id = ++pid; p.TeamId = team.Id; p.Condition = 100; }
            clubs.Add(team);
        }
        return clubs;
    }

    private static MatchSetup Setup(Team home, Team away, Tuning? tuning = null)
    {
        // The clubs are shared between matches in a run; an injury from the last one must not
        // carry into the next.
        foreach (var p in home.Players.Concat(away.Players)) p.IsInjured = false;

        var hs = new SquadSelection { TeamId = home.Id };
        hs.AutoPickBestSquad(home.Players);
        var aws = new SquadSelection { TeamId = away.Id };
        aws.AutoPickBestSquad(away.Players);
        return new MatchSetup(home, away, hs, aws, MatchTactics.From(home), MatchTactics.From(away), 1.04, Tuning: tuning);
    }

    private static double Strength(Team t)
        => t.Players.Select(p => p.Overall100).OrderByDescending(x => x).Take(10).Average();

    [Fact]
    public void AMatchIsPlayedToTheEnd()
    {
        var clubs = AllClubs.Value;
        var home = clubs.First(c => c.Name == "Győr");
        var away = clubs.First(c => c.Name == "Metz Handball");
        var match = new PhysicsMatch(Setup(home, away), seed: 7);

        var sw = Stopwatch.StartNew();
        match.RunToEnd();
        sw.Stop();

        output.WriteLine($"{home.Name} {match.Score[0]}–{match.Score[1]} {away.Name} in {sw.ElapsedMilliseconds} ms");
        foreach (var g in match.Events.GroupBy(e => e.Kind).OrderBy(g => g.Key))
            output.WriteLine($"  {g.Key}: {g.Count()}");

        Assert.True(match.IsFinished);
        Assert.InRange(match.Score[0] + match.Score[1], 20, 110);
    }

    /// <summary>Where the goals come from. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void ShotDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var shots = new List<ShotRecord>();
        var attacks = new List<AttackRecord>();
        var events = new List<MatchEvent2D>();
        var causes = new Dictionary<string, int>();
        var rng = new Random(3);
        for (int i = 0; i < 40; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            match.RunToEnd();
            shots.AddRange(match.Shots);
            attacks.AddRange(match.Attacks.Where(a => a.End > 0));
            events.AddRange(match.Events);
            foreach (var (k, v) in match.TurnoverCauses) causes[k] = causes.GetValueOrDefault(k) + v;
        }
        output.WriteLine("Turnovers/team: " + string.Join(", ", causes.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value / 40.0:F1}")));
        output.WriteLine(string.Join(", ", events.GroupBy(e => e.Kind).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count() / 80.0:F1}")) + "  (per team)");
        output.WriteLine($"{attacks.Count / 40.0:F1} attacks/team, {attacks.Average(a => a.Duration):F1} s, {attacks.Average(a => a.Passes):F1} passes, "
            + $"{attacks.Count(a => a.Shots > 0) * 100.0 / attacks.Count:F0}% reach a shot, first shot at {attacks.Where(a => a.FirstShotAt >= 0).Average(a => a.FirstShotAt):F1} s");
        foreach (var g in attacks.GroupBy(a => a.StartedBy))
            output.WriteLine($"  from {g.Key,-10} n={g.Count(),4} {g.Average(a => a.Duration):F1} s {g.Average(a => a.Passes):F1} passes, first shot {g.Where(a => a.FirstShotAt >= 0).Select(a => (double)a.FirstShotAt).DefaultIfEmpty(0).Average():F1} s");
        output.WriteLine($"{shots.Count} shots in 20 matches");
        output.WriteLine($"Per match: Kempa tries {events.Count(e => e.Kind == EventKind.KempaAttempt) / 40.0:F2}, Kempa goals {shots.Count(s => s.Kempa && s.Outcome == ShotOutcome.Goal) / 40.0:F2}, "
            + $"long open-net shots {shots.Count(s => s.Distance > 14f) / 40.0:F2} (scored {shots.Count(s => s.Distance > 14f && s.Outcome == ShotOutcome.Goal) / 40.0:F2}), "
            + $"injuries {events.Count(e => e.Kind == EventKind.Injury) / 40.0:F2}, time-outs {events.Count(e => e.Kind == EventKind.Timeout) / 40.0:F2}, subs {events.Count(e => e.Kind == EventKind.Substitution) / 40.0:F1}");
        foreach (var g in shots.GroupBy(s => s.SevenMetre ? "7m" : s.Distance < 7 ? "<7m" : s.Distance < 9 ? "7-9m" : ">=9m"))
        {
            output.WriteLine($"{g.Key,-5} n={g.Count(),4} goal {g.Count(s => s.Outcome == ShotOutcome.Goal) * 100.0 / g.Count(),3:F0}%  "
                + $"xg {g.Average(s => s.Xg):F2}  pressure {g.Average(s => s.Pressure):F2}  sigma {g.Average(s => s.Sigma):F2}  "
                + $"keeper u {g.Average(s => s.KeeperFrame.X):F1} |w| {g.Average(s => Math.Abs(s.KeeperFrame.Y)):F1}  "
                + string.Join(" ", g.GroupBy(s => s.Outcome).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}")));
        }
        foreach (var g in shots.Where(s => !s.SevenMetre).GroupBy(s => s.Role))
            output.WriteLine($"  {g.Key} got the ball by: " + string.Join(", ", g.GroupBy(s => s.GotBallBy).Select(x => $"{x.Key} {x.Count() * 100 / g.Count()}%")));
        foreach (var g in shots.Where(s => !s.SevenMetre && s.Role is "CB" or "LB").GroupBy(s => s.Role))
            output.WriteLine($"  context {g.Key}: attack <8s {g.Count(s => s.AttackAge < 8) * 100 / g.Count()}%, after drive {g.Count(s => s.AfterDrive) * 100 / g.Count()}%, "
                + $"held >1.5s {g.Count(s => s.HeldFor > 1.5f) * 100 / g.Count()}%, median age {g.OrderBy(s => s.AttackAge).ElementAt(g.Count() / 2).AttackAge:F0}s");
        foreach (var g in shots.Where(s => !s.SevenMetre).GroupBy(s => s.Role))
            output.WriteLine($"  {g.Key,-6} n={g.Count(),4} dist {g.Average(s => s.Distance):F1} goal {g.Count(s => s.Outcome == ShotOutcome.Goal) * 100.0 / g.Count():F0}%  xg {g.Average(s => s.Xg):F2}  "
                + $"keeper u {g.Average(s => s.KeeperFrame.X):F1} w {g.Average(s => s.KeeperFrame.Y * Math.Sign(s.From.Y - 10) * (s.Side == 0 ? 1 : 1)):F1}  aimW {g.Average(s => s.GoalW):F2}  "
                + string.Join(" ", g.GroupBy(s => s.Outcome).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}")));
    }

    /// <summary>
    /// How many goals are credited with an assist, against how long the scorer had held the ball.
    /// Real stat sheets assist roughly two goals in five. Inert unless HM_ENGINE2D_CAL=1.
    /// </summary>
    [Fact]
    public void AssistDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var rng = new Random(5);
        var goals = new List<ShotRecord>();
        int allGoals = 0, assists = 0;
        for (int i = 0; i < 60; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            match.RunToEnd();
            goals.AddRange(match.Shots.Where(s => s.Outcome == ShotOutcome.Goal));
            allGoals += match.Score[0] + match.Score[1];
            assists += match.Stats.Values.Sum(s => s.Assists);
        }

        var open = goals.Where(g => !g.SevenMetre).ToList();
        output.WriteLine($"{allGoals} goals, {open.Count} in open play; assists credited {assists} = {assists * 100.0 / allGoals:F0}% of all goals");
        output.WriteLine("open-play goals by how the scorer got the ball: "
            + string.Join(", ", open.GroupBy(g => g.GotBallBy).Select(g => $"{g.Key} {g.Count() * 100 / open.Count}%")));
        var passed = open.Where(g => g.GotBallBy == "pass").ToList();
        foreach (float w in new[] { 0.8f, 1.0f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f })
            output.WriteLine($"  held <= {w:F2} s after a pass: {passed.Count(g => g.HeldFor <= w) * 100.0 / allGoals:F0}% of all goals");
        var held = passed.Select(g => g.HeldFor).OrderBy(x => x).ToList();
        if (held.Count > 0)
            output.WriteLine($"  held for: p25 {held[held.Count / 4]:F2} s, median {held[held.Count / 2]:F2} s, p75 {held[held.Count * 3 / 4]:F2} s");
    }

    /// <summary>
    /// The match ratings the 2D engine's lines earn, by position, against the 7.0 mean the scale
    /// was calibrated to on the quick sim. Writes the lines to HM_ENGINE2D_LINES as CSV if set.
    /// Inert unless HM_ENGINE2D_CAL=1.
    /// </summary>
    [Fact]
    public void RatingDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var rng = new Random(9);
        var lines = new List<(string Pos, int Goals, int Assists, int Shots, int Saves, int Against, int Margin, int Two, int Red, double Rating)>();
        for (int i = 0; i < 80; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            match.RunToEnd();
            var byId = home.Players.Concat(away.Players).ToDictionary(p => p.Id);
            foreach (var st in match.Stats.Values)
            {
                if (!byId.TryGetValue(st.PlayerId, out var p)) continue;
                int side = st.TeamId == home.Id ? 0 : 1;
                int margin = match.Score[side] - match.Score[1 - side];
                string pos = st.Saves > 0 || st.GoalsAgainst > 0 ? "GK" : p.Position;
                lines.Add((pos, st.Goals, st.Assists, st.Shots, st.Saves, st.GoalsAgainst, margin, st.TwoMinutes, st.RedCards,
                    MatchRating.For(pos, st, margin, MatchRating.Scale.Physics2D)));
            }
        }

        foreach (var g in lines.GroupBy(l => l.Pos).OrderBy(g => g.Key))
        {
            var r = g.Select(l => l.Rating).ToList();
            double mean = r.Average(), sd = Math.Sqrt(r.Average(x => (x - mean) * (x - mean)));
            int shots = g.Sum(l => l.Shots), goals = g.Sum(l => l.Goals);
            output.WriteLine($"{g.Key,-6} n={g.Count(),5} rating {mean:F2} sd {sd:F2}  shots/line {shots / (double)g.Count():F2} goals/line {goals / (double)g.Count():F2} "
                + $"miss rate {(shots == 0 ? 0 : 1 - goals / (double)shots):F2}  assists/line {g.Average(l => l.Assists):F2}  under 6: {g.Count(l => l.Rating < 6) * 100.0 / g.Count():F0}%");
        }

        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_LINES") is { } path)
        {
            var csv = new System.Text.StringBuilder("position,goals,assists,shots,saves,against,margin,two_min,red,rating\n");
            foreach (var l in lines)
                csv.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"{l.Pos},{l.Goals},{l.Assists},{l.Shots},{l.Saves},{l.Against},{l.Margin},{l.Two},{l.Red},{l.Rating:F2}\n");
            File.WriteAllText(path, csv.ToString());
        }
    }

    /// <summary>The first minutes of a match, event by event. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void PlayByPlay()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var home = clubs.First(c => c.Name == "Győr");
        var away = clubs.First(c => c.Name == "Metz Handball");
        var match = new PhysicsMatch(Setup(home, away), seed: 11);
        while (match.Clock < 300 && !match.IsFinished) match.Tick();
        var rows = match.Events.Select(e => (e.Time, Text: $"{e.Kind} {(e.Side >= 0 ? match.Sides[e.Side].Team.Name : "")} {e.PlayerName} / {e.OtherPlayerName}"))
            .Concat(match.Attacks.Select(a => (a.Start, Text: $"   >> attack {match.Sides[a.Side].Team.Name} from {a.StartedBy}, {a.Passes} passes, {a.Shots} shots, lasted {a.End - a.Start:F1}")))
            .Concat(match.Shots.Select(s => (s.Time, Text: $"      shot {s.Role} from {s.Distance:F1} m xg {s.Xg:F2} -> {s.Outcome}")))
            .OrderBy(r => r.Item1);
        foreach (var (t, text) in rows) output.WriteLine($"{t,6:F1} {text}");
    }

    /// <summary>A knockout tie always has a winner: extra time, then seven-metres, never a draw.</summary>
    [Fact]
    public void AKnockoutTieAlwaysProducesAWinner()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(9);
        int extraTime = 0;
        for (int i = 0; i < 12; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var setup = Setup(home, away) with { IsKnockout = true };
            var match = new PhysicsMatch(setup, seed: rng.Next());
            match.RunToEnd();
            if (match.IsExtraTime) extraTime++;
            IPlayedMatch played = match;
            bool decided = played.HomeScore != played.AwayScore || played.HomeShootoutScore != played.AwayShootoutScore;
            Assert.True(decided, $"{home.Name} v {away.Name} ended level");
        }
        output.WriteLine($"{extraTime} of 12 went to extra time");
    }

    /// <summary>What the season files for a watched match: the event log reads like a match report.</summary>
    [Fact]
    public void TheResultReadsLikeAMatchReport()
    {
        var clubs = AllClubs.Value;
        var match = new PhysicsMatch(Setup(clubs.First(c => c.Name == "Győr"), clubs.First(c => c.Name == "Metz Handball")), seed: 3);
        match.RunToEnd();
        IPlayedMatch played = match;
        var log = played.EventLog.ToList();
        Assert.Equal(played.HomeScore + played.AwayScore, log.Count(e => e.EventType == "Goal"));
        Assert.All(log, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
        Assert.True(played.CourtMinutes.Values.Sum() > 60 * 13, "fourteen players' worth of minutes");
        foreach (var e in log.Where(e => e.EventType == "Goal").Take(8)) output.WriteLine($"{e.Minute}' {e.Description}");
    }

    /// <summary>
    /// Writes a stretch of a match, tick by tick, for the HTML replay viewer
    /// (tools/replay_viewer.py). Inert unless HM_ENGINE2D_REPLAY names the output file.
    /// </summary>
    [Fact]
    public void ExportReplay()
    {
        string? path = Environment.GetEnvironmentVariable("HM_ENGINE2D_REPLAY");
        if (string.IsNullOrEmpty(path)) return;
        string homeName = Environment.GetEnvironmentVariable("HM_ENGINE2D_HOME") ?? "Győr";
        string awayName = Environment.GetEnvironmentVariable("HM_ENGINE2D_AWAY") ?? "Metz Handball";
        float from = float.TryParse(Environment.GetEnvironmentVariable("HM_ENGINE2D_FROM"), out var f) ? f : 0f;
        float seconds = float.TryParse(Environment.GetEnvironmentVariable("HM_ENGINE2D_SECONDS"), out var sec) ? sec : 300f;

        var clubs = AllClubs.Value;
        var home = clubs.First(c => c.Name == homeName);
        var away = clubs.First(c => c.Name == awayName);
        var match = new PhysicsMatch(Setup(home, away), seed: 21);

        // The seed files carry no logo path — the seeder assigns those — so name them here.
        string homeLogo = Environment.GetEnvironmentVariable("HM_ENGINE2D_HOMELOGO") ?? "Hungary/gyor.png";
        string awayLogo = Environment.GetEnvironmentVariable("HM_ENGINE2D_AWAYLOGO") ?? "France/metz.png";
        var kits = KitColours.Resolve(homeLogo, awayLogo);
        var theme = CourtTheme.App;

        while (match.Clock < from && !match.IsFinished) { if (match.IsHalfTime) match.StartSecondHalf(); match.Tick(); }

        var frames = new List<object>();
        var events = new List<object>();
        int seen = match.Events.Count;
        var rng = new Random(1);
        while (match.Clock < from + seconds && !match.IsFinished)
        {
            if (match.IsHalfTime) match.StartSecondHalf();
            match.Tick();
            frames.Add(new
            {
                t = Math.Round(match.Clock, 1),
                s = match.Score.ToArray(),
                o = match.Possession,
                r = match.Referees.Select(r => new[] { Math.Round(r.Pos.X, 2), Math.Round(r.Pos.Y, 2) }).ToArray(),
                st = (int)match.Ball.State,
                b = new[] { Math.Round(match.Ball.Pos.X, 2), Math.Round(match.Ball.Pos.Y, 2), Math.Round(match.Ball.Height, 2), match.Ball.Holder?.Id ?? 0 },
                p = match.Agents.Where(a => a.State != AgentState.Bench)
                    .Select(a => new object[] { a.Id, Math.Round(a.Pos.X, 2), Math.Round(a.Pos.Y, 2), (int)a.State }).ToArray(),
            });
            while (seen < match.Events.Count)
            {
                var e = match.Events[seen++];
                if (e.Kind is EventKind.FreeThrow or EventKind.ThrowOff) continue;
                events.Add(new { t = Math.Round(e.Time, 1), side = e.Side, text = Commentary.Describe(match, e, rng) });
            }
        }

        var players = match.Agents.Select(a => new { id = a.Id, side = a.Side, gk = a.IsKeeper, n = a.Player.ShirtNumber, name = a.Name, role = a.Role });
        var doc = new
        {
            home = home.Name, away = away.Name,
            kits = new { home = kits.Home.Shirt, homeNum = kits.Home.Number, away = kits.Away.Shirt, awayNum = kits.Away.Number,
                         homeGk = kits.HomeKeeper.Shirt, awayGk = kits.AwayKeeper.Shirt },
            theme,
            players, frames, events,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(doc));
        output.WriteLine($"{frames.Count} frames, {events.Count} events -> {path}");
    }

    /// <summary>What players choose with the ball, by role. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void DecisionDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var traces = new List<DecisionTrace>();
        var passes = new List<(string From, string To, float Length)>();
        var rng = new Random(4);
        for (int i = 0; i < 8; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next()) { Trace = [] };
            match.RunToEnd();
            traces.AddRange(match.Trace!);
        }
        string[] kinds = ["keep", "shoot", "pass", "drive", "kempa"];
        foreach (var g in traces.Where(t => t.Role != "GK").GroupBy(t => t.Role).OrderBy(g => g.Key))
        {
            var chose = g.GroupBy(t => t.Chosen == 2 ? "pass→" + t.ChosenTarget : kinds[t.Chosen]).OrderByDescending(x => x.Count())
                .Select(x => $"{x.Key} {x.Count() * 100.0 / g.Count():F0}%");
            output.WriteLine($"{g.Key,-6} n={g.Count(),5}: {string.Join(", ", chose)}");
        }
        foreach (var t in traces.Where(t => t.Role == "LW" && t.Chosen == 1).Take(10))
            output.WriteLine($"  LW shoots at ({t.At.U:F1},{t.At.W:F1}) {t.Time:F0}s keep {t.Keep:F2} | " + string.Join("  ", t.Options.Select(o => $"{kinds[o.Kind]}{(o.Target == "" ? "" : ">" + o.Target)} {o.Value:F2}")));

        // The pivot with a good look: what did she do, and what were the numbers?
        var pivotLooks = traces.Where(t => t.Role == "Pivot" && t.Options.Any(o => o.Kind == 1 && o.Value > 0.55f)).ToList();
        output.WriteLine($"Pivot with a shot worth > 0.55: {pivotLooks.Count}, shot {pivotLooks.Count(t => t.Chosen == 1)}, " +
                         $"passed {pivotLooks.Count(t => t.Chosen == 2)}, kept {pivotLooks.Count(t => t.Chosen == 0)}");
        foreach (var t in pivotLooks.Where(t => t.Chosen != 1).Take(12))
            output.WriteLine($"  {t.Time:F0}s keep {t.Keep:F2} | " + string.Join("  ", t.Options.Select(o => $"{kinds[o.Kind]}{(o.Target == "" ? "" : ">" + o.Target)} {o.Value:F2}/{o.Noisy:F2}")) + $"  => {kinds[t.Chosen]} {t.ChosenTarget}");
    }

    /// <summary>How tired the starters get, and how often the benches are used. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void RotationDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var rng = new Random(8);
        for (int i = 0; i < 4; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            var starters = match.Sides[0].Court.ToList();
            string line = "";
            foreach (float at in new[] { 600f, 1200f, 1800f, 2400f, 3000f, 3599f })
            {
                while (match.Clock < at && !match.IsFinished) { if (match.IsHalfTime) match.StartSecondHalf(); match.Tick(); }
                line += $" {at / 60:F0}': " + string.Join(",", starters.Where(a => !a.IsKeeper).Select(a => ((int)a.Energy).ToString()));
            }
            match.RunToEnd();
            output.WriteLine($"{home.Name}: subs {match.Events.Count(e => e.Kind == EventKind.Substitution && e.Side == 0)} |{line}");
            output.WriteLine("   minutes: " + string.Join(", ", match.Sides[0].Squad.Where(a => a.CourtSeconds > 0).Select(a => $"{a.Role}:{a.CourtSeconds / 60:F0}")));
        }
    }

    private sealed class ManualTicker : IFrameTicker
    {
        public event EventHandler? Tick;
        public void Start() { }
        public void Stop() { }
        public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// At 32× the court stops for the goals: every goal is shown at normal speed, the scoreboard
    /// and the feed never run ahead of what is on screen, and the match still gets to the end.
    /// </summary>
    [Fact]
    public void FastForwardStopsForTheMoments()
    {
        var clubs = AllClubs.Value;
        foreach (int seed in new[] { 5, 6, 7, 8, 9, 10 })
            PlayFastForward(clubs, seed);
    }

    private void PlayFastForward(List<Team> clubs, int seed)
    {
        var match = new PhysicsMatch(Setup(clubs.First(c => c.Name == "Győr"), clubs.First(c => c.Name == "Metz Handball")) with { UserSide = 0 }, seed: seed);
        var look = new MatchPresentation("", "", CourtTheme.App, KitColours.Resolve("", ""));
        double now = 0;
        var ticker = new ManualTicker();
        var vm = new Match2DViewModel(match, look, 0, _ => { }, ticker, () => now);
        vm.SetSpeedCommand.Execute("32");

        int replayFrames = 0, replays = 0; bool wasReplaying = false;
        for (int frame = 0; frame < 200_000 && !vm.IsFinished; frame++)
        {
            now += 1.0 / 30;
            if (vm.IsHalfTime) vm.ContinueSecondHalfCommand.Execute(null);
            if (vm.IsChoosingTaker) vm.ChooseTaker(0);
            ticker.Fire();

            if (vm.IsReplayingMoment)
            {
                replayFrames++;
                if (!wasReplaying) replays++;
                Assert.True(vm.To.Time <= match.Clock + 0.01f, "the court shows the past, never the future");
                Assert.True(vm.HomeScore + vm.AwayScore <= match.Score[0] + match.Score[1]);
            }
            wasReplaying = vm.IsReplayingMoment;
            if (vm.Feed.Count > 0) Assert.True(vm.Feed[0].Minute <= (int)(vm.To.Time / 60f) + 1,
                $"the feed never runs ahead of the court: '{vm.Feed[0].Text}' at {vm.Feed[0].Minute}' with the court at {vm.To.Time:F1}s, match at {match.Clock:F1}s, replaying {vm.IsReplayingMoment}");
        }

        output.WriteLine($"{replays} moments replayed over {replayFrames} frames; {match.Score[0]}–{match.Score[1]}");
        Assert.True(vm.IsFinished);
        Assert.True(replays >= (match.Score[0] + match.Score[1]) / 2, "most goals are shown");
    }

    /// <summary>
    /// The time-out button at 32×, pressed while the other side has the ball: it used to do
    /// nothing. Now the request waits, the match drops to 1×, the time-out is given the moment
    /// the ball is ours, both sides gather in a ring in front of their bench, and play goes on.
    /// </summary>
    [Fact]
    public void ATimeOutWaitsForTheBallAndTheSideHuddles()
    {
        var clubs = AllClubs.Value;
        var match = new PhysicsMatch(Setup(clubs.First(c => c.Name == "Győr"), clubs.First(c => c.Name == "Metz Handball")) with { UserSide = 0 }, seed: 11);
        var look = new MatchPresentation("", "", CourtTheme.App, KitColours.Resolve("", ""));
        double now = 0;
        var ticker = new ManualTicker();
        var vm = new Match2DViewModel(match, look, 0, _ => { }, ticker, () => now);
        vm.SetSpeedCommand.Execute("32");

        void Frame()
        {
            now += 1.0 / 30;
            if (vm.IsChoosingTaker) vm.ChooseTaker(0);
            // Half-time waits for SECOND HALF, as a player would press it.
            if (vm.IsHalfTime) vm.ContinueSecondHalfCommand.Execute(null);
            ticker.Fire();
        }

        // Into the match, then to a moment the ball is the other side's.
        for (int f = 0; f < 3000; f++) Frame();
        for (int f = 0; f < 20_000 && !(match.Possession == 1 && match.Phase == MatchPhase.Play); f++) Frame();
        Assert.True(match.Possession == 1, $"the other side's ball by now: clock {match.Clock:F0} s, phase {match.Phase}, restart {match.Restart}, possession {match.Possession}");

        vm.CallTimeoutCommand.Execute(null);
        Assert.Equal(1, vm.Speed);
        Assert.True(vm.TimeoutRequested || match.TimeoutSide == 0, "the request is either granted or waiting");

        bool huddled = false, granted = false;
        for (int f = 0; f < 30_000 && !(granted && match.Phase != MatchPhase.Timeout); f++)
        {
            Frame();
            if (match.Phase != MatchPhase.Timeout || match.TimeoutSide != 0) continue;
            granted = true;
            var court = match.Sides[0].Court;
            var centre = new System.Numerics.Vector2(court.Average(a => a.Pos.X), court.Average(a => a.Pos.Y));
            if (court.All(a => System.Numerics.Vector2.Distance(a.Pos, centre) < 2.6f)) huddled = true;
        }

        output.WriteLine($"granted {granted}, huddled {huddled}, time-outs left {match.Sides[0].TimeoutsLeft}");
        Assert.True(granted, "the waiting time-out was never given");
        Assert.True(huddled, "the players never gathered at the bench");
        Assert.False(vm.TimeoutRequested);
        Assert.Equal(2, match.Sides[0].TimeoutsLeft);

        // A second request withdrawn before it is granted costs nothing.
        for (int f = 0; f < 20_000 && !(match.Possession == 1 && match.Phase == MatchPhase.Play); f++) Frame();
        vm.CallTimeoutCommand.Execute(null);
        if (vm.TimeoutRequested)
        {
            vm.CallTimeoutCommand.Execute(null);
            Assert.False(vm.TimeoutRequested);
            Assert.Equal(2, match.Sides[0].TimeoutsLeft);
        }
    }

    /// <summary>
    /// With the manager's rotation switched off, the bench makes no changes on that side
    /// beyond replacing the injured; the other bench rotates as before.
    /// </summary>
    [Fact]
    public void WithAutoSubstitutionsOffOnlyInjuriesAreReplaced()
    {
        var clubs = AllClubs.Value;
        var home = clubs.First(c => c.Name == "Győr");
        var away = clubs.First(c => c.Name == "Metz Handball");

        int Subs(PhysicsMatch m, int side) => m.Events.Count(e => e.Kind == EventKind.Substitution && e.Side == side);
        int Injuries(PhysicsMatch m, int side) => m.Events.Count(e => e.Kind == EventKind.Injury && e.Side == side);

        var on = new PhysicsMatch(Setup(home, away) with { UserSide = 0 }, seed: 21);
        on.RunToEnd();
        var off = new PhysicsMatch(Setup(home, away) with { UserSide = 0 }, seed: 21) { UserAutoSubstitutions = false };
        off.RunToEnd();

        output.WriteLine($"rotation on: {Subs(on, 0)} changes; off: {Subs(off, 0)} changes, {Injuries(off, 0)} injuries; other bench {Subs(off, 1)}");
        Assert.True(Subs(on, 0) > 0);
        Assert.True(Subs(off, 0) <= Injuries(off, 0), "a change was made that nobody asked for");
        Assert.True(Subs(off, 1) > 0, "the other bench still rotates");
    }

    /// <summary>
    /// A keeper never stands on the ball, and the restart after a goal is sometimes quick — the
    /// two things the first playtest on a phone caught.
    /// </summary>
    [Fact]
    public void KeepersReleaseAndThrowOffsVary()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(12);
        int fast = 0, slow = 0; float longest = 0;
        for (int i = 0; i < 8; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            match.RunToEnd();
            fast += match.FastThrowOffs; slow += match.SlowThrowOffs;
            longest = MathF.Max(longest, match.LongestKeeperHold);
        }
        output.WriteLine($"throw-offs after goals: {fast} fast, {slow} slow; longest keeper hold {longest:F1} s");
        Assert.True(longest < 6f, $"a keeper held the ball {longest:F1} s");
        Assert.InRange(fast / (double)(fast + slow), 0.2, 0.8);
    }

    /// <summary>
    /// A keeper goes for the shot: a tester saw them stand still while it went past, at seven
    /// metres above all. On a save she ends up where the ball arrives; on a goal she has moved
    /// towards it; and at seven metres she sometimes goes early, the wrong way included.
    /// </summary>
    [Fact]
    public void TheKeeperDivesAtTheShot()
    {
        var clubs = AllClubs.Value;
        var match = new PhysicsMatch(Setup(clubs.First(c => c.Name == "Győr"), clubs.First(c => c.Name == "Metz Handball")), seed: 5);
        var moved = new List<float>();
        var saveGaps = new List<float>();
        var sevenDives = new List<float>();
        Agent? keeper = null;
        System.Numerics.Vector2 from = default, to = default;
        ShotOutcome pending = default;
        bool seven = false;

        while (!match.IsFinished)
        {
            if (match.IsHalfTime) match.StartSecondHalf();
            bool wasShot = match.Ball.State == BallState.Shot;
            match.Tick();
            if (wasShot && match.Ball.State != BallState.Shot && keeper != null)
            {
                // It has landed: where her dive took her.
                float d = System.Numerics.Vector2.Distance(from, keeper.Pos);
                if (pending is ShotOutcome.Goal or ShotOutcome.Saved or ShotOutcome.SavedHeld) moved.Add(d);
                if (pending is ShotOutcome.Saved or ShotOutcome.SavedHeld) saveGaps.Add(System.Numerics.Vector2.Distance(keeper.Pos, to));
                if (seven) sevenDives.Add(d);
                keeper = null;
            }
            if (!wasShot && match.Ball.State == BallState.Shot && match.Ball.ShotKeeper is { } k && match.Ball.PendingShot != ShotOutcome.Blocked)
            {
                keeper = k; from = k.Pos; to = match.Ball.To; pending = match.Ball.PendingShot; seven = match.Ball.IsSevenMetre;
            }
        }

        output.WriteLine($"{moved.Count} shots on target: moved {moved.Average():F2} m on average; saves {saveGaps.Count}, gap {saveGaps.DefaultIfEmpty().Average():F2} m; "
            + $"seven-metres {sevenDives.Count}, {sevenDives.Count(d => d > 0.6f)} with a dive");
        Assert.True(moved.Average() > 0.4f, $"keepers go for the ball: {moved.Average():F2} m on average");
        // Not every one: a wing landing beside her can push her off the spot as the ball arrives.
        Assert.True(saveGaps.Count > 0 && saveGaps.Count(g => g < 0.5f) >= 0.9 * saveGaps.Count,
            $"a save is made where the ball arrives: {saveGaps.Count(g => g >= 0.5f)} of {saveGaps.Count} more than half a metre off");
        Assert.Contains(sevenDives, d => d > 0.6f);
    }

    /// <summary>
    /// The second half starts as the first did, from the centre: the ends change, the side that
    /// did not throw off at the start throws off now, and each team is lined up in its own half.
    /// </summary>
    [Fact]
    public void SecondHalfStartsWithAThrowOffFromTheCentre()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(31);
        for (int i = 0; i < 4; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            int first = match.Possession;
            float homeAttackedFirst = match.Sides[0].AttackGoalX;
            while (!match.IsHalfTime && !match.IsFinished) match.Tick();
            Assert.True(match.IsHalfTime);

            match.StartSecondHalf();

            Assert.Equal(MatchPhase.Restart, match.Phase);
            Assert.Equal(RestartKind.ThrowOff, match.Restart);
            Assert.Equal(1 - first, match.Possession);
            Assert.Equal(Court.Length - homeAttackedFirst, match.Sides[0].AttackGoalX);
            Assert.InRange(match.Ball.Pos.X, Court.CentreX - 0.3f, Court.CentreX + 0.3f);
            Assert.InRange(match.Ball.Pos.Y, Court.CentreY - 0.3f, Court.CentreY + 0.3f);
            foreach (var s in match.Sides)
                foreach (var a in s.Court)
                {
                    // Own half: the half away from the goal the side now attacks.
                    float towardsAttack = (a.Pos.X - Court.CentreX) * s.Dir;
                    Assert.True(towardsAttack <= 0.3f, $"{a.Role} of side {s.Index} starts {towardsAttack:F1} m into the other half");
                }
        }
    }

    /// <summary>
    /// A manager can make several changes at once: each replacement walks out to the line and
    /// waits, in view, and goes on when the player she replaces is off.
    /// </summary>
    [Fact]
    public void SeveralSubstitutionsCanBeUnderWayAtOnce()
    {
        var clubs = AllClubs.Value;
        var home = clubs.First(c => c.Name == "Győr");
        var away = clubs.First(c => c.Name == "Metz Handball");
        var match = new PhysicsMatch(Setup(home, away) with { UserSide = 0 }, seed: 3);
        match.Advance(120f);

        var side = match.Sides[0];
        var off = side.Court.Where(a => !a.IsKeeper && a != match.Ball.Holder).Take(2).ToList();
        var on = side.Squad.Where(a => a.State == AgentState.Bench && !a.IsKeeper).Take(2).ToList();
        Assert.True(match.RequestSubstitution(0, off[0], on[0]));
        Assert.True(match.RequestSubstitution(0, off[1], on[1]));
        Assert.Equal(2, side.Changes.Count);
        // Nobody can be sent on twice.
        Assert.False(match.RequestSubstitution(0, side.Court.First(a => !a.IsKeeper && a != match.Ball.Holder), on[0]));

        match.Advance(1f);
        Assert.All(on, a => Assert.True(a.OnTouchline));

        for (int i = 0; i < 300 && side.IsChanging; i++) { if (match.IsHalfTime) match.StartSecondHalf(); match.Tick(); }
        Assert.False(side.IsChanging);
        Assert.All(on, a => Assert.Equal(AgentState.OnCourt, a.State));
        Assert.All(off, a => Assert.Equal(AgentState.Bench, a.State));
        Assert.Equal(2, match.Events.Count(e => e.Kind == EventKind.Substitution && e.Side == 0 && on.Any(a => a.Id == e.PlayerId)));
    }

    /// <summary>
    /// A seven-metre against the managed side stops for its keeper to be named; one from the
    /// bench comes on through the zone, and the throw waits until she is in her goal.
    /// </summary>
    [Fact]
    public void TheManagerNamesTheKeeperForASevenMetreAgainst()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(5);
        int swaps = 0, pulled = 0;
        for (int n = 0; n < 40; n++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var setup = Setup(home, away) with { UserSide = 0 };
            // Half of them playing seven against six, which leaves the goal empty when the ball is lost.
            setup.HomeTactics.UseEmptyGoal = n % 2 == 1;
            var match = new PhysicsMatch(setup, seed: rng.Next()) { AskForSevenMetreTaker = true };
            while (!match.IsFinished)
            {
                if (match.IsHalfTime) match.StartSecondHalf();
                if (match.AwaitingTaker) match.ChooseSevenMetreTaker(0);
                if (match.AwaitingKeeper)
                {
                    var keepers = match.SevenMetreKeepers(0).ToList();
                    var inGoal = match.Sides[0].Keeper;
                    if (inGoal != null) Assert.Equal(inGoal, keepers[0]); else pulled++;
                    // A keeper from the bench, not the one who was in goal last.
                    var specialist = keepers.Last(k => k.State == AgentState.Bench);
                    match.ChooseSevenMetreKeeper(specialist.Id);
                    Assert.False(match.AwaitingChoice);

                    // The throw is not taken until the specialist is standing in her goal.
                    int guard = 0;
                    while (match.Phase == MatchPhase.Restart && guard++ < 600)
                    {
                        if (inGoal != null) Assert.False(inGoal.State == AgentState.OnCourt && specialist.State == AgentState.OnCourt);
                        match.Tick();
                    }
                    Assert.Equal(specialist, match.Sides[0].Keeper);
                    var (u, w) = PhysicsMatch.Frame(match.Sides[1], specialist.Pos);
                    Assert.True(u < 2.2f && MathF.Abs(w) < 1.2f, $"the throw was taken with the keeper {u:F1} m out, at w {w:F1}");
                    swaps++;
                    continue;
                }
                match.Tick();
            }
        }
        output.WriteLine($"{swaps} keeper changes for a seven-metre, {pulled} with the goal empty when it was given");
        Assert.True(swaps >= 3, $"only {swaps} seven-metres against the managed side with a keeper on the bench");
    }

    /// <summary>
    /// No restart may hold the match up: every one is taken within seconds of its whistle.
    /// A goal throw given to a side playing seven against six once went to a court player whose
    /// spot was inside the goal area, and she stood at the line for the rest of the match while
    /// the clock ran. Half of these play seven against six to go looking for the same thing.
    /// </summary>
    [Fact]
    public void NoRestartStallsTheMatch()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(3);
        float longest = 0; string where = ""; int sevens = 0;
        for (int n = 0; n < 60; n++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var setup = Setup(home, away);
            setup.HomeTactics.UseEmptyGoal = setup.AwayTactics.UseEmptyGoal = n % 2 == 1;
            var m = new PhysicsMatch(setup, seed: rng.Next());
            float since = -1;
            bool sevenSetUp = false;
            while (!m.IsFinished)
            {
                if (m.IsHalfTime) m.StartSecondHalf();
                bool wasSeven = m.Phase == MatchPhase.Restart && m.Restart == RestartKind.SevenMetre;
                int thrower = m.Possession;
                m.Tick();
                // Every seven-metre, whoever wins it: never at an empty or unready goal.
                if (wasSeven && m.Phase == MatchPhase.Play)
                {
                    var keeper = m.Sides[1 - thrower].Keeper;
                    Assert.NotNull(keeper);
                    var (ku, kw) = PhysicsMatch.Frame(m.Sides[thrower], keeper!.Pos);
                    Assert.True(ku < 2.2f && MathF.Abs(kw) < 1.2f, $"a seven-metre was taken with the keeper {ku:F1} m out, at w {kw:F1}");
                    sevenSetUp = true;
                }
                if (m.Phase == MatchPhase.Restart) { if (since < 0) since = m.Clock; } else since = -1;
                if (since >= 0 && m.Clock - since > longest) { longest = m.Clock - since; where = $"{m.Restart} in {home.Name} v {away.Name}"; }
                if (sevenSetUp) { sevens++; sevenSetUp = false; }
            }
        }
        output.WriteLine($"longest restart {longest:F1} s ({where})");
        Assert.True(sevens > 20, $"only {sevens} seven-metres checked");
        // A keeper walking on for a seven-metre is the slowest honest restart there is.
        Assert.True(longest < 40f, $"a restart took {longest:F0} s: {where}");
    }

    /// <summary>
    /// A save is caught, tipped over, pushed aside or knocked back into play — each ending the
    /// way the rules say — and a block now and then goes back over the defence's own line for a
    /// throw from the corner.
    /// </summary>
    [Fact]
    public void SavesAndBlocksEndUpWhereTheyShould()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(9);
        int held = 0, goalThrows = 0, loose = 0, corners = 0;
        for (int n = 0; n < 25; n++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var m = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            int seen = 0; EventKind? pending = null; var prev = m.Phase;
            while (!m.IsFinished)
            {
                if (m.IsHalfTime) m.StartSecondHalf();
                m.Tick();
                for (; seen < m.Events.Count; seen++)
                    if (m.Events[seen].Kind is EventKind.Save or EventKind.Block) pending = m.Events[seen].Kind;
                if (pending == null) { prev = m.Phase; continue; }
                if (m.Phase == MatchPhase.Restart && prev != MatchPhase.Restart)
                {
                    if (pending == EventKind.Save && m.Restart == RestartKind.GoalThrow) goalThrows++;
                    if (pending == EventKind.Block && m.Restart == RestartKind.ThrowIn) corners++;
                    pending = null;
                }
                else if (m.Ball.State == BallState.Held && m.Phase == MatchPhase.Play)
                {
                    if (pending == EventKind.Save) { if (m.Ball.Holder!.IsKeeper) held++; else loose++; }
                    pending = null;
                }
                prev = m.Phase;
            }
        }
        output.WriteLine($"after saves: {held} held, {goalThrows} goal throws, {loose} won from a rebound; {corners} corner throws off blocks");
        Assert.True(held > goalThrows && goalThrows > 0 && loose > 0 && corners > 0);
    }

    [Fact]
    public void TheSameSeedPlaysTheSameMatch()
    {
        var clubs = AllClubs.Value;
        var a = new PhysicsMatch(Setup(clubs[0], clubs[1]), seed: 42);
        var b = new PhysicsMatch(Setup(clubs[0], clubs[1]), seed: 42);
        a.RunToEnd();
        b.RunToEnd();
        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Events.Count, b.Events.Count);
    }

    /// <summary>
    /// The quick sim's answer to "how often does the stronger side win", from one real season of
    /// every league — the curve the physics engine has to reproduce, or a managed club's matches
    /// would be easier or harder than everybody else's. Inert unless HM_ENGINE2D_REF=1.
    /// </summary>
    [Fact]
    public async Task QuickSimReferenceCurve()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_REF") != "1") return;
        using var world = await SeasonWorld.CreateAsync(HandballManager.Services.LeagueService.LigaFlorilorCompetition, seed: 5);
        var strength = (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(world.Db.Players))
            .Where(p => p.TeamId != null)
            .GroupBy(p => p.TeamId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Overall100).OrderByDescending(x => x).Take(10).Average());

        await world.PlaySeasonAsync();

        var buckets = new Dictionary<int, (int N, double Points)>();
        int n = 0, homeWins = 0, draws = 0; double goals = 0;
        foreach (var m in world.Db.MatchRecords.ToList())
        {
            if (!strength.TryGetValue(m.HomeTeamId, out var hs) || !strength.TryGetValue(m.AwayTeamId, out var aws)) continue;
            if (m.HomeGoals + m.AwayGoals == 0) continue; // walkovers
            n++; goals += m.HomeGoals + m.AwayGoals;
            if (m.HomeGoals > m.AwayGoals) homeWins++; else if (m.HomeGoals == m.AwayGoals) draws++;
            int gap = Math.Clamp((int)Math.Round((hs - aws) / 3.0), -4, 4);
            var b = buckets.GetValueOrDefault(gap);
            buckets[gap] = (b.N + 1, b.Points + (m.HomeGoals > m.AwayGoals ? 1 : m.HomeGoals == m.AwayGoals ? 0.5 : 0));
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Quick sim, one season: {n} matches, {goals / n:F1} goals, home wins {homeWins * 100.0 / n:F0}%, draws {draws * 100.0 / n:F0}%");
        foreach (var (gap, (cnt, pts)) in buckets.OrderBy(k => k.Key))
            sb.AppendLine($"  gap {gap * 3,+3}: {pts / cnt:P0} of {cnt}");
        output.WriteLine(sb.ToString());
        string outDir = Environment.GetEnvironmentVariable("HM_ENGINE2D_OUT") ?? Path.GetTempPath();
        File.WriteAllText(Path.Combine(outDir, "quicksim-reference-curve.txt"), sb.ToString());
    }

    /// <summary>
    /// The anchors, pinned. Tolerances are wide because forty matches are a small sample, but a
    /// change that moves the engine off the numbers the whole game is balanced on fails here.
    /// Re-calibrated 2026-09-29 to real women's handball (see ScoringModel) rather than the old
    /// engine's 62.9: about 57 goals, margins averaging 8.5, a side past 40 in about 6% of
    /// matches, 3 suspensions a team at a normal line, the pace down to about 125 attacks.
    /// </summary>
    [Fact]
    public void TheEngineStaysCalibrated()
    {
        var clubs = AllClubs.Value;
        var rng = new Random(2027);
        int n = 0; double goals = 0, sevens = 0, twoMin = 0;
        for (int i = 0; i < 40; i++)
        {
            var home = clubs[rng.Next(clubs.Count)];
            var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
            var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
            match.RunToEnd();
            n++;
            goals += match.Score[0] + match.Score[1];
            sevens += match.Events.Count(e => e.Kind == EventKind.SevenMetreAwarded);
            twoMin += match.Events.Count(e => e.Kind is EventKind.TwoMinutes or EventKind.RedCard);
        }
        output.WriteLine($"{goals / n:F1} goals, {sevens / n / 2:F2} seven-metres and {twoMin / n / 2:F2} suspensions per team");
        Assert.InRange(goals / n, 52.0, 63.0);
        Assert.InRange(sevens / n / 2, 2.3, 5.5);
        Assert.InRange(twoMin / n / 2, 1.8, 4.2);
    }

    /// <summary>
    /// The same fixture, twice: from a real save world, the way the game sets a watched match up,
    /// and from the seed files, the way the calibration does. Inert unless HM_ENGINE2D_CAL=1.
    /// </summary>
    [Fact]
    public async Task InGameFixtureAgainstSeedFixture()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        using var world = await SeasonWorld.CreateAsync(HandballManager.Services.LeagueService.LigaFlorilorCompetition, seed: 3);
        var teams = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(world.Db.Teams, t => t.Players), t => t.Manager));
        var home = teams.First(t => t.Name.Contains("Bistri"));
        var away = teams.First(t => t.Name.Contains("Bra"));
        output.WriteLine($"{home.Name} v {away.Name}; conditions {home.Players.Average(p => p.Condition):F0}/{away.Players.Average(p => p.Condition):F0}, "
            + $"morale {home.Players.Average(p => p.Morale):F0}/{away.Players.Average(p => p.Morale):F0}");

        var rng = new Random(1);
        double gameGoals = 0;
        for (int i = 0; i < 20; i++)
        {
            var hs = new SquadSelection { TeamId = home.Id }; hs.AutoPickBestSquad(home.Players);
            var aws = new SquadSelection { TeamId = away.Id }; aws.AutoPickBestSquad(away.Players);
            var setup = new MatchSetup(home, away, hs, aws, MatchTactics.From(home),
                HandballManager.Services.TacticsRules.ForAi(away, 0, 0, rng), 1.05, UserSide: 0);
            var match = new PhysicsMatch(setup, seed: rng.Next());
            match.RunToEnd();
            foreach (var p in home.Players.Concat(away.Players)) p.IsInjured = false;
            gameGoals += match.Score[0] + match.Score[1];
        }

        var clubs = AllClubs.Value;
        var h2 = clubs.First(c => c.Name.Contains("Bistri"));
        var a2 = clubs.First(c => c.Name.Contains("Bra") && c.Nation == h2.Nation);
        double seedGoals = 0;
        for (int i = 0; i < 20; i++)
        {
            var match = new PhysicsMatch(Setup(h2, a2), seed: rng.Next());
            match.RunToEnd();
            seedGoals += match.Score[0] + match.Score[1];
        }
        output.WriteLine($"from the save: {gameGoals / 20:F1} goals a match; from the seed files: {seedGoals / 20:F1}");
    }

    /// <summary>Goals a match in each league, domestic fixtures only. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void GoalsByLeague()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        var rng = new Random(6);
        foreach (var nation in clubs.Select(c => c.Nation).Distinct().OrderBy(n => n))
        {
            var pool = clubs.Where(c => c.Nation == nation).ToList();
            if (pool.Count < 2) continue;
            double goals = 0, saves = 0, shots = 0; int n = 0;
            var totals = new List<int>(); var margins = new List<int>();
            for (int i = 0; i < 24; i++)
            {
                var h = pool[rng.Next(pool.Count)];
                var a = pool.Where(c => c != h).OrderBy(_ => rng.Next()).First();
                var m = new PhysicsMatch(Setup(h, a), seed: rng.Next());
                m.RunToEnd();
                goals += m.Score[0] + m.Score[1];
                totals.Add(m.Score[0] + m.Score[1]); margins.Add(Math.Abs(m.Score[0] - m.Score[1]));
                saves += m.Events.Count(e => e.Kind is EventKind.Save or EventKind.SevenMetreSaved);
                shots += m.Shots.Count;
                n++;
            }
            var gks = pool.SelectMany(c => c.Players.Where(p => p.Position == "GK")).ToList();
            var outs = pool.SelectMany(c => c.Players.Where(p => p.Position != "GK")).ToList();
            double mean = totals.Average(), sd = Math.Sqrt(totals.Average(t => (t - mean) * (t - mean)));
            output.WriteLine($"{nation,-14} {goals / n,5:F1} goals (sd {sd:F1}, margin {margins.Average():F1})  save% {saves / shots * 100,4:F0}  "
                + $"GK reflexes {gks.Average(p => p.Reflexes):F1} reach {gks.Average(p => p.AerialReach):F1} agility {gks.Average(p => p.Agility):F1} height {gks.Average(p => p.Height):F0}  "
                + $"outfield finishing {outs.Average(p => p.Finishing):F1} longthrows {outs.Average(p => p.LongThrows):F1}");
        }
    }

    /// <summary>
    /// The calibration report under several tunings at once, for calibrating. HM_ENGINE2D_GRID is
    /// a list of variants separated by ';', each a list of field=value pairs on Tuning.
    /// </summary>
    [Fact]
    public void CalibrationGrid()
    {
        var grid = Environment.GetEnvironmentVariable("HM_ENGINE2D_GRID");
        if (string.IsNullOrEmpty(grid)) return;
        int matches = int.TryParse(Environment.GetEnvironmentVariable("HM_ENGINE2D_MATCHES"), out int m) ? m : 150;
        foreach (var variant in grid.Split(';'))
        {
            var t = new Tuning();
            foreach (var pair in variant.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                var f = typeof(Tuning).GetField(kv[0].Trim())!;
                f.SetValue(t, f.FieldType == typeof(int) ? int.Parse(kv[1]) : (object)float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            output.WriteLine($"=== {variant}");
            output.WriteLine(Report(matches, t));
        }
    }

    /// <summary>Two-minute suspensions and red cards by defensive instruction, both sides alike. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void DisciplineByAggression()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        var clubs = AllClubs.Value;
        foreach (var aggression in new[] { DefensiveAggression.Contain, DefensiveAggression.Normal, DefensiveAggression.Aggressive })
        {
            var rng = new Random(4);
            double twoMin = 0, reds = 0, sevens = 0; int n = 0;
            for (int i = 0; i < 60; i++)
            {
                var home = clubs[rng.Next(clubs.Count)];
                var away = clubs.Where(c => c.Nation == home.Nation && c != home).OrderBy(_ => rng.Next()).First();
                // The home side is the managed one: the AI's bench would put its own club's
                // instruction back within the minute. Only its fouls are counted.
                var setup = Setup(home, away) with { UserSide = 0 };
                setup.HomeTactics.Aggression = aggression;
                var m = new PhysicsMatch(setup, seed: rng.Next());
                m.RunToEnd();
                twoMin += 2 * m.Events.Count(e => e.Side == 0 && e.Kind is EventKind.TwoMinutes or EventKind.RedCard && e.Side == 0);
                reds += 2 * m.Events.Count(e => e.Kind == EventKind.RedCard && e.Side == 0);
                sevens += 2 * m.Events.Count(e => e.Kind == EventKind.SevenMetreAwarded && e.Side == 1);
                n++;
            }
            output.WriteLine($"{aggression,-10} 2-min/team {twoMin / (2.0 * n):F2}, red/match (two such sides) {reds / n:F2}, 7m conceded/team {sevens / (2.0 * n):F2}   (quick sim: 2.2 / 3.2 / 4.1)");
        }
    }

    /// <summary>
    /// A badly mismatched fixture with one outstanding player — the shape of a 29–49 reported from
    /// a 2028 save, CSM Târgu Jiu v CSM Corona Brașov, with one back scoring 30. Clubs are cloned
    /// and inflated the way several seasons of progression might leave them. Inert unless
    /// HM_ENGINE2D_CAL=1.
    /// </summary>
    [Fact]
    public void MismatchDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        int n = int.TryParse(Environment.GetEnvironmentVariable("HM_ENGINE2D_MATCHES"), out int m) ? m : 40;

        Team Fresh(string part) => Clone(AllClubs.Value.First(c => c.Name.Contains(part)));

        var scenarios = new (string Label, Func<(Team Home, Team Away)> Build)[]
        {
            ("seed squads", () => (Fresh("Jiu"), Fresh("Corona"))),
            ("star back at 95", () => { var a = Fresh("Corona"); Lift(StarOf(a), 95); return (Fresh("Jiu"), a); }),
            ("star 97, Corona +12", () => { var a = Fresh("Corona"); LiftAll(a, 12); Lift(StarOf(a), 97); return (Fresh("Jiu"), a); }),
            ("star 97, Corona +12, Jiu -8", () => { var a = Fresh("Corona"); LiftAll(a, 12); Lift(StarOf(a), 97); var h = Fresh("Jiu"); LiftAll(h, -8); return (h, a); }),
        };

        foreach (var (label, build) in scenarios)
        {
            var (home, away) = build();
            output.WriteLine($"=== {label}: {home.Name} top-10 {Strength(home):F1} v {away.Name} top-10 {Strength(away):F1}, star {StarOf(away).Overall100}");
            var rng = new Random(11);
            var totals = new List<int>(); var awaySide = new List<int>();
            var topShots = new List<int>(); var topGoals = new List<int>(); var topShare = new List<double>();
            for (int i = 0; i < n; i++)
            {
                var match = new PhysicsMatch(Setup(home, away), seed: rng.Next());
                match.RunToEnd();
                totals.Add(match.Score[0] + match.Score[1]);
                awaySide.Add(match.Score[1]);
                var mine = match.Stats.Values.Where(s => s.TeamId == away.Id).ToList();
                var top = mine.OrderByDescending(s => s.Shots).First();
                topShots.Add(top.Shots); topGoals.Add(top.Goals);
                topShare.Add(top.Shots / (double)Math.Max(1, mine.Sum(s => s.Shots)));
            }
            double mean = totals.Average(), sd = Math.Sqrt(totals.Average(x => (x - mean) * (x - mean)));
            output.WriteLine($"  goals {mean:F1} (sd {sd:F1}, max {totals.Max()}), away side {awaySide.Average():F1} (max {awaySide.Max()}), "
                + $"top shooter {topShots.Average():F1} shots / {topGoals.Average():F1} goals (max {topShots.Max()} / {topGoals.Max()}), share {topShare.Average():P0} (max {topShare.Max():P0})");
        }
    }

    private static Team Clone(Team t)
    {
        var json = JsonSerializer.Serialize(t, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
        var copy = JsonSerializer.Deserialize<Team>(json)!;
        foreach (var p in copy.Players) p.Team = null;
        return copy;
    }

    /// <summary>The club's best back: in the seeded Corona Brașov, the one who scored 30 in 2028.</summary>
    private static Player StarOf(Team t)
        => t.Players.FirstOrDefault(p => p.LastName.Contains("Boiciuc"))
           ?? t.Players.Where(p => p.Position is "LB" or "RB" or "CB").OrderByDescending(p => p.Overall100).First();

    private static readonly string[] TrainedAttributes =
    [
        "Dribbling", "Finishing", "LongThrows", "Marking", "SevenMeterTaking", "Tackling", "Technique", "Receiving", "Passing",
        "AerialReach", "Communication", "Eccentricity", "Handling", "Throwing", "OneOnOnes", "Reflexes",
        "Aggression", "Anticipation", "Composure", "Concentration", "Decisions", "Determination", "Flair", "Leadership",
        "OffTheBall", "Positioning", "Teamwork", "Vision", "Acceleration", "Agility", "Balance", "JumpingReach",
        "NaturalFitness", "Pace", "Stamina", "Strength"
    ];

    /// <summary>Raises (or lowers) every attribute a step at a time until the rating reaches the target.</summary>
    private static void Lift(Player p, int targetOverall)
    {
        int dir = targetOverall >= p.Overall100 ? 1 : -1;
        for (int guard = 0; guard < 40 && (dir > 0 ? p.Overall100 < targetOverall : p.Overall100 > targetOverall); guard++)
            foreach (var name in TrainedAttributes)
                if (typeof(Player).GetProperty(name) is { } prop && prop.PropertyType == typeof(int))
                    prop.SetValue(p, Math.Clamp((int)prop.GetValue(p)! + dir, 1, 20));
    }

    private static void LiftAll(Team t, int by)
    {
        foreach (var p in t.Players) Lift(p, Math.Clamp(p.Overall100 + by, 10, 99));
    }

    /// <summary>The numbers the engine has to hit. Inert unless HM_ENGINE2D_CAL=1.</summary>
    [Fact]
    public void CalibrationReport()
    {
        if (Environment.GetEnvironmentVariable("HM_ENGINE2D_CAL") != "1") return;
        int matches = int.TryParse(Environment.GetEnvironmentVariable("HM_ENGINE2D_MATCHES"), out int m) ? m : 120;
        output.WriteLine(Report(matches, null));
    }

    internal static string Report(int matches, Tuning? tuning, int seed = 1)
    {
        var clubs = AllClubs.Value;
        var rng = new Random(seed);
        var sw = Stopwatch.StartNew();
        double goals = 0, shots = 0, saves = 0, misses = 0, blocks = 0, sevens = 0, sevenGoals = 0, twoMin = 0,
               turnovers = 0, interceptions = 0, freeThrows = 0, passiveFt = 0, subs = 0, homeWins = 0, draws = 0;
        var gapBuckets = new Dictionary<int, (int N, double Points)>();
        var totals = new List<int>();
        var margins = new List<int>();
        var sides = new List<int>();
        var perMatch = new List<(int Goals, int Attacks, int Shots, int Fast, int Early)>();
        var topShots = new List<int>();
        var topShare = new List<double>();
        var bands = new Dictionary<string, (int N, int Goals, int Saves)>();
        double sevenSaves = 0;

        for (int i = 0; i < matches; i++)
        {
            // Mostly league opponents, as a season is.
            var home = clubs[rng.Next(clubs.Count)];
            var pool = clubs.Where(c => c != home && c.Nation == home.Nation).ToList();
            var away = pool.Count > 0 && rng.NextDouble() < 0.8 ? pool[rng.Next(pool.Count)] : clubs[rng.Next(clubs.Count)];
            if (away == home) continue;

            var setup = Setup(home, away, tuning);
            // HM_ENGINE2D_TEMPO=Counter|Controlled forces the home side's tempo (HM_ENGINE2D_BOTH=1 both sides),
            // to check a managed side's instructions against the numbers.
            if (Enum.TryParse<AttackTempo>(Environment.GetEnvironmentVariable("HM_ENGINE2D_TEMPO"), out var tempo))
            {
                setup.HomeTactics.Tempo = tempo;
                if (Environment.GetEnvironmentVariable("HM_ENGINE2D_BOTH") == "1") setup.AwayTactics.Tempo = tempo;
                setup = setup with { UserSide = 0 };
            }
            var match = new PhysicsMatch(setup, seed: rng.Next());
            match.RunToEnd();

            goals += match.Score[0] + match.Score[1];
            totals.Add(match.Score[0] + match.Score[1]);
            margins.Add(Math.Abs(match.Score[0] - match.Score[1]));
            sides.Add(match.Score[0]); sides.Add(match.Score[1]);
            perMatch.Add((match.Score[0] + match.Score[1], match.Attacks.Count, match.Shots.Count,
                match.FastThrowOffs, match.Shots.Count(x => x.AttackAge < 8f)));
            foreach (int side in new[] { 0, 1 })
            {
                int teamId = side == 0 ? home.Id : away.Id;
                var mine = match.Stats.Values.Where(x => x.TeamId == teamId).ToList();
                int most = mine.Count == 0 ? 0 : mine.Max(x => x.Shots);
                topShots.Add(most);
                topShare.Add(most / (double)Math.Max(1, mine.Sum(x => x.Shots)));
            }
            var ev = match.Events;
            shots += match.Shots.Count(s => !s.SevenMetre);
            saves += ev.Count(e => e.Kind == EventKind.Save);
            misses += ev.Count(e => e.Kind is EventKind.Miss or EventKind.Post);
            blocks += ev.Count(e => e.Kind == EventKind.Block);
            sevens += ev.Count(e => e.Kind == EventKind.SevenMetreAwarded);
            sevenGoals += ev.Count(e => e.Kind == EventKind.SevenMetreGoal);
            sevenSaves += ev.Count(e => e.Kind == EventKind.SevenMetreSaved);
            foreach (var shot in match.Shots.Where(x => !x.SevenMetre && x.Distance <= 14f))
            {
                string band = shot.Distance < 7f ? "<7m" : shot.Distance < 9f ? "7-9m" : "9m+";
                var bs = bands.GetValueOrDefault(band);
                bands[band] = (bs.N + 1, bs.Goals + (shot.Outcome == ShotOutcome.Goal ? 1 : 0),
                               bs.Saves + (shot.Outcome is ShotOutcome.Saved or ShotOutcome.SavedHeld ? 1 : 0));
            }
            twoMin += ev.Count(e => e.Kind is EventKind.TwoMinutes or EventKind.RedCard);
            turnovers += ev.Count(e => e.Kind == EventKind.Turnover);
            interceptions += ev.Count(e => e.Kind == EventKind.Interception);
            freeThrows += ev.Count(e => e.Kind == EventKind.FreeThrow);
            passiveFt += ev.Count(e => e.Kind == EventKind.PassivePlay);
            subs += ev.Count(e => e.Kind == EventKind.Substitution);
            if (match.Score[0] > match.Score[1]) homeWins++;
            else if (match.Score[0] == match.Score[1]) draws++;

            int gap = (int)Math.Round((Strength(home) - Strength(away)) / 3.0);
            gap = Math.Clamp(gap, -4, 4);
            var b = gapBuckets.GetValueOrDefault(gap);
            double pts = match.Score[0] > match.Score[1] ? 1 : match.Score[0] == match.Score[1] ? 0.5 : 0;
            gapBuckets[gap] = (b.N + 1, b.Points + pts);
        }
        sw.Stop();

        double perTeam(double x) => x / matches / 2;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{matches} matches in {sw.Elapsed.TotalSeconds:F1} s ({sw.Elapsed.TotalMilliseconds / matches:F0} ms each)");
        sb.AppendLine($"Goals/match          {goals / matches:F1}   (anchor 62.9)");
        double mean = totals.Average(), sd = Math.Sqrt(totals.Average(x => (x - mean) * (x - mean)));
        var sorted = totals.OrderBy(x => x).ToList();
        var bySize = perMatch.OrderBy(x => x.Goals).ToList();
        foreach (var (label, slice) in new[] { ("lowest 10%", bySize.Take(bySize.Count / 10)), ("highest 10%", bySize.Skip(bySize.Count * 9 / 10)) })
        {
            var l = slice.ToList();
            sb.AppendLine($"  {label}: {l.Average(x => x.Goals):F0} goals, {l.Average(x => x.Attacks):F0} attacks, {l.Average(x => x.Shots):F0} shots, "
                + $"{l.Average(x => x.Goals / (double)Math.Max(1, x.Shots)) * 100:F0}% scored, fast throw-offs {l.Average(x => x.Fast):F0}, shots in first 8 s {l.Average(x => x.Early):F0}");
        }
        var ms = margins.OrderBy(x => x).ToList();
        sb.AppendLine($"Margins              mean {ms.Average():F1}, p90 {ms[(int)(0.9 * (ms.Count - 1))]}, max {ms[^1]} | side≥40 {sides.Count(x => x >= 40) * 100.0 / sides.Count:F1}%, max side {sides.Max()}   (real: 6–7, p90 14–15, max ~25, side≥40 1–6%, max ~49)");
        var ts = topShots.OrderBy(x => x).ToList();
        sb.AppendLine($"Top shooter/team     {ts.Average():F1} shots (p90 {ts[(int)(0.9 * (ts.Count - 1))]}, max {ts[^1]}), share {topShare.Average():P0} (p90 {topShare.OrderBy(x => x).ElementAt((int)(0.9 * (topShare.Count - 1))):P0})   (real: about 25%, rarely past 40%)");
        sb.AppendLine($"Save rate            {saves / Math.Max(1, saves + goals - sevenGoals) * 100:F1}% of open-play shots on target");
        sb.AppendLine($"Match totals         sd {sd:F1}, 5th pct {sorted[sorted.Count / 20]}, 95th pct {sorted[sorted.Count * 19 / 20]}, max {sorted[^1]}, over 80: {totals.Count(x => x > 80) * 100.0 / totals.Count:F0}%");
        sb.AppendLine($"Open-play shots/team {perTeam(shots):F1}, saves {perTeam(saves):F1}, misses+posts {perTeam(misses):F1}, blocks {perTeam(blocks):F1}");
        sb.AppendLine($"Open-play shooting   {(goals - sevenGoals) / Math.Max(1, shots) * 100:F0}%");
        sb.AppendLine($"7m/team              {perTeam(sevens):F2}, scored {sevenGoals / Math.Max(1, sevens) * 100:F0}%, saved {sevenSaves / Math.Max(1, sevens) * 100:F0}%   (real ~74% scored, ~20% saved)");
        sb.AppendLine("Shooting by distance " + string.Join("   ", new[] { "<7m", "7-9m", "9m+" }.Where(bands.ContainsKey)
            .Select(k => $"{k} {bands[k].Goals * 100.0 / bands[k].N:F0}% scored, {bands[k].Saves * 100.0 / bands[k].N:F0}% saved ({bands[k].N * 1.0 / matches / 2:F1}/team)"))
            + "   (real: 6 m ~60–65%, 9 m ~38–42%)");
        sb.AppendLine($"2-min/team           {perTeam(twoMin):F2}   (anchor 3.7)");
        sb.AppendLine($"Turnovers/team       {perTeam(turnovers):F1} (+ interceptions {perTeam(interceptions):F1}), free throws {perTeam(freeThrows):F1}, passive {perTeam(passiveFt):F2}");
        sb.AppendLine($"Substitutions/team   {perTeam(subs):F1}");
        sb.AppendLine($"Home wins {homeWins / matches * 100:F0}%, draws {draws / matches * 100:F0}%");
        sb.AppendLine("Home points share by strength gap (top-10 overall, per 3 points):");
        foreach (var (gap, (n, p)) in gapBuckets.OrderBy(k => k.Key))
            sb.AppendLine($"  gap {gap * 3,+3}: {p / n:P0} of {n}");
        return sb.ToString();
    }
}
