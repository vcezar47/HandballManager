using System.Collections.ObjectModel;
using HandballManager.Models;
using System.Linq;

namespace HandballManager.Services;

public class LiveMatchEngine
{
    private readonly Random _rng = new();
    
    // Config / Context
    public Team HomeTeam { get; }
    public Team AwayTeam { get; }
    public SquadSelection HomeSquad { get; }
    public SquadSelection AwaySquad { get; }
    public double HomeAdvantage { get; }
    public string VenueName { get; }
    
    // Core State
    public int GameMinute { get; private set; } = 0;
    public double GameSecond { get; private set; } = 0;
    public int HomeScore { get; private set; } = 0;
    public int AwayScore { get; private set; } = 0;
    public int HomeShootoutScore { get; private set; } = 0;
    public int AwayShootoutScore { get; private set; } = 0;
    public int HomeTimeoutsRemaining { get; private set; } = 3;
    public int AwayTimeoutsRemaining { get; private set; } = 3;
    public bool IsHalfTime { get; private set; } = false;
    public bool IsFullTime { get; private set; } = false;
    public bool IsOvertime { get; private set; } = false;
    public bool IsExtraTimeHalfTime { get; private set; } = false;
    public bool IsShootout { get; private set; } = false;
    public bool IsKnockout { get; private set; } = false;
    private bool _inExtraTime = false;
    private int _extraTimeStartMinute = 60;

    // Match Statistics Tracking
    public int HomeShots { get; private set; } = 0;
    public int AwayShots { get; private set; } = 0;
    public int HomeShotsOnTarget { get; private set; } = 0;
    public int AwayShotsOnTarget { get; private set; } = 0;
    private long _homePossessionTicks = 0;
    private long _totalPossessionTicks = 0;

    // Trackers
    public Dictionary<int, double> PlayerEnergy { get; } = new();
    public Dictionary<int, MatchPlayerStat> Stats { get; } = new();
    public ObservableCollection<LiveMatchEvent> EventLog { get; } = new();

    // Engine State
    public bool HomeHasPossession { get; private set; } = true;
    public string CurrentPhase { get; private set; } = "BuildUp"; 
    
    // NEW PASING LOGIC
    private double _currentAttackTime = 0;
    private double _requiredAttackTime = 15.0;
    private int _lastEnergyUpdateMinute = -1;
    private double _secondsSinceLastFlavor = 0;

    private readonly HashSet<int> _playersOnCourt = new();
    
    public LiveMatchEngine(Team home, Team away, SquadSelection homeSquad, SquadSelection awaySquad, double homeAdvantage, string venueName, bool isKnockout = false)
    {
        HomeTeam = home;
        AwayTeam = away;
        HomeSquad = homeSquad;
        AwaySquad = awaySquad;
        HomeAdvantage = homeAdvantage;
        VenueName = venueName;
        IsKnockout = isKnockout;

        foreach (var p in HomeTeam.Players.Concat(AwayTeam.Players))
        {
            p.MatchEnergy = 100.0;
            PlayerEnergy[p.Id] = 100.0;
        }

        // Starters are marked as having played immediately
        foreach (var p in HomeSquad.StartingLineup.Values
                        .Concat(AwaySquad.StartingLineup.Values)
                        .Where(p => p != null))
        {
            _playersOnCourt.Add(p!.Id);
            if (!Stats.ContainsKey(p.Id))
                Stats[p.Id] = new MatchPlayerStat { PlayerId = p.Id, PlayerName = p.Name, TeamId = p.TeamId ?? 0 };
        }

        ResetRequiredAttackTime();
    }

    private void ResetRequiredAttackTime()
    {
        // Handball attacks take 20-50s typically
        _requiredAttackTime = 20.0 + _rng.NextDouble() * 30.0;
        _currentAttackTime = 0;
        _secondsSinceLastFlavor = 0;
        CurrentPhase = "BuildUp";

        // 10% chance for a Fast Break (resolve instantly)
        if (_rng.NextDouble() < 0.10) _requiredAttackTime = 5.0; 
    }

    public void Tick(double seconds)
    {
        if (IsFullTime && !IsOvertime) return;

        // Advance clock
        GameSecond += seconds;
        _currentAttackTime += seconds;
        _secondsSinceLastFlavor += seconds;
        
        // Possession tracking
        _totalPossessionTicks++;
        if (HomeHasPossession) _homePossessionTicks++;

        // Match Half/Full time logic
        HandleTimeLimits();

        if (IsHalfTime || IsFullTime) return;

        // Flavor checks during Build Up
        if (_currentAttackTime < _requiredAttackTime * 0.4) CurrentPhase = "BuildUp";
        else if (_currentAttackTime < _requiredAttackTime * 0.8) CurrentPhase = "Probing";
        else CurrentPhase = "Finishing";

        if (_secondsSinceLastFlavor >= 8.0)
        {
            _secondsSinceLastFlavor = 0;
            TriggerFlavorEvent();
        }

        // Resolution check
        if (_currentAttackTime >= _requiredAttackTime)
        {
            ResolveAttack();
            ResetRequiredAttackTime();

            // AI Tactical Check every 5 possessions
            if (_rng.NextDouble() < 0.2)
            {
                AutoSubstitutionForAI(HomeTeam, HomeSquad);
                AutoSubstitutionForAI(AwayTeam, AwaySquad);
            }
        }

        // Energy drain (Once per minute)
        if (GameMinute > _lastEnergyUpdateMinute && GameMinute > 0)
        {
            _lastEnergyUpdateMinute = GameMinute;
            UpdateEnergyFromMinutePassed();
        }
    }

    public double HomePossessionPercent => _totalPossessionTicks > 0 ? (double)_homePossessionTicks / _totalPossessionTicks * 100 : 50;

    private void TriggerFlavorEvent()
    {
        var attSquad = HomeHasPossession ? HomeSquad : AwaySquad;
        var p = attSquad.StartingLineup.Values.Where(v => v != null && v.Position != "GK").ToList();
        if (!p.Any()) return;
        var player = p[_rng.Next(p.Count)]!;

        string[] msgs = ["passes the ball out to the wing.", "looks for an opening in the defense.", "signals for a tactical maneuver.", "bounces the ball, looking for a gap.", "recycles the possession."];
        TriggerEvent("Flavor", player, $"{player.Name} {msgs[_rng.Next(msgs.Length)]}");
    }

    private void ResolveAttack()
    {
        SquadSelection attSquad = HomeHasPossession ? HomeSquad : AwaySquad;
        SquadSelection defSquad = HomeHasPossession ? AwaySquad : HomeSquad;

        var attackers = attSquad.StartingLineup.Values.Where(p => p != null && p.Position != "GK").ToList();
        var defenders = defSquad.StartingLineup.Values.Where(p => p != null && p.Position != "GK").ToList();
        var gk = defSquad.StartingLineup["GK"];

        if (gk == null) { TogglePossession(); return; }

        double attackRating = attackers.Average(p => p != null ? (p.Finishing + p.Passing + p.Technique + p.Decisions + p.Pace + p.Acceleration) / 6.0 : 0);
        double defenseRating = defenders.Average(p => p != null ? (p.Marking + p.Tackling + p.Positioning + p.Strength + p.Aggression + p.Anticipation) / 6.0 : 0);
        
        double currentBonus = HomeHasPossession ? HomeAdvantage : 1.0;
        double diff = (attackRating * currentBonus) - defenseRating;

        // SimulationEngine Logic: Shot Chance (Reduced from 0.72 to 0.69 to avoid excessive goals)
        double shotChance = Math.Clamp(0.69 + (diff / 35.0), 0.35, 0.95);

        if (_rng.NextDouble() > shotChance)
        {
            // Turnover
            var stealer = defenders[_rng.Next(defenders.Count)]!;
            TriggerEvent("Turnover", stealer, $"{stealer.Name} intercepts the ball! Possession changes.");
            TogglePossession();
            return;
        }

        // Resolve Shot
        if (HomeHasPossession) HomeShots++; else AwayShots++;
        
        var shooter = attackers[_rng.Next(attackers.Count)]!;
        if (Stats.ContainsKey(shooter.Id)) Stats[shooter.Id].Shots++;

        // Determine if shot is ON TARGET
        // Usually 90%+ in handball, but technique and fatigue matter
        double accuracy = Math.Clamp(0.92 + (shooter.Technique - 15) / 100.0 - (1.0 - PlayerEnergy[shooter.Id]/100.0)*0.1, 0.7, 0.98);
        bool isOnTarget = _rng.NextDouble() < accuracy;

        if (!isOnTarget)
        {
            TriggerEvent("Miss", shooter, $"WIDE! {shooter.Name} finds a gap but the shot sails just past the post!");
            TogglePossession();
            return;
        }

        if (HomeHasPossession) HomeShotsOnTarget++; else AwayShotsOnTarget++;
        
        if (gk == null) { ScoreGoal(shooter, null, null); TogglePossession(); return; }

        double gkRating = (gk.Reflexes * 2.5 + gk.OneOnOnes * 2.0 + gk.Handling + gk.Positioning * 1.5 + gk.Anticipation) / 8.0;
        
        // SimulationEngine Logic: Goal Chance (Reduced base from 0.82 to 0.77 for realism)
        double goalChance = Math.Clamp(0.77 + (shooter.Finishing - gkRating) / 25.0, 0.40, 0.96);

        if (_rng.NextDouble() < goalChance)
        {
            var assister = attackers.FirstOrDefault(p => p != null && p.Id != shooter.Id);
            ScoreGoal(shooter, assister, gk);
        }
        else
        {
            // Removed redundant HomeShotsOnTarget++ here to fix the double count bug
            if (Stats.ContainsKey(gk.Id)) Stats[gk.Id].Saves++;
            TriggerEvent("Save", gk, $"KEEPER SAVES! {gk.Name} denies {shooter.Name} with a massive reflex save!");
        }

        TogglePossession();
    }

    private void ScoreGoal(Player scorer, Player? assistant, Player? gk)
    {
        if (scorer == null) return;
        if (Stats.ContainsKey(scorer.Id)) Stats[scorer.Id].Goals++;
        if (assistant != null && Stats.ContainsKey(assistant.Id)) Stats[assistant.Id].Assists++;
        if (gk != null && Stats.ContainsKey(gk.Id)) Stats[gk.Id].GoalsAgainst++;

        if (HomeHasPossession) HomeScore++;
        else AwayScore++;

        string msg = assistant != null 
            ? $"GOAL! {scorer.Name} scores! Beautiful pass by {assistant.Name}." 
            : $"GOAL! {scorer.Name} puts it away with pure clinical power!";
            
        TriggerEvent("Goal", scorer, msg);
    }

    private void TriggerEvent(string type, Player? primaryPlayer, string desc)
    {
        if (primaryPlayer == null) return;
        EventLog.Insert(0, new LiveMatchEvent 
        { 
            Minute = GameMinute + 1, Second = (int)GameSecond, 
            EventType = type, TeamId = primaryPlayer.TeamId ?? 0, 
            PlayerId = primaryPlayer.Id, PlayerName = primaryPlayer.Name,
            Description = desc 
        });
    }

    private void TogglePossession()
    {
        HomeHasPossession = !HomeHasPossession;
        _secondsSinceLastFlavor = 0;
    }

    private void HandleTimeLimits()
    {
        double totalMinutes = GameMinute + GameSecond / 60.0;

        if (!_inExtraTime)
        {
            if (GameMinute < 30 && totalMinutes >= 30)
            {
                GameMinute = 30; GameSecond = 0; IsHalfTime = true;
                return;
            }
            if (GameMinute < 60 && totalMinutes >= 60)
            {
                GameMinute = 60; GameSecond = 0; IsFullTime = true;
                return;
            }
        }
        else
        {
            // Extra time: two 5-minute halves starting from minute 60
            int etHalf1End = _extraTimeStartMinute + 5;
            int etFullEnd  = _extraTimeStartMinute + 10;

            if (!IsExtraTimeHalfTime && GameMinute < etHalf1End && totalMinutes >= etHalf1End)
            {
                GameMinute = etHalf1End; GameSecond = 0;
                IsExtraTimeHalfTime = true;
                return;
            }
            if (!IsFullTime && GameMinute < etFullEnd && totalMinutes >= etFullEnd)
            {
                GameMinute = etFullEnd; GameSecond = 0;
                IsFullTime = true;
                return;
            }
        }

        while (GameSecond >= 60) { GameSecond -= 60; GameMinute++; }
    }

    /// <summary>Starts 2x5 min extra time after a knockout draw at 60 min.</summary>
    public void StartExtraTime()
    {
        IsFullTime = false;
        IsOvertime = true;
        _inExtraTime = true;
        _extraTimeStartMinute = GameMinute;
        ResetRequiredAttackTime();
        EventLog.Insert(0, new LiveMatchEvent { Minute = GameMinute, EventType = "Status", Description = "EXTRA TIME — 1st Half begins!" });
    }

    /// <summary>Resumes extra time 2nd half after the break.</summary>
    public void StartExtraTimeSecondHalf()
    {
        IsExtraTimeHalfTime = false;
        ResetRequiredAttackTime();
        EventLog.Insert(0, new LiveMatchEvent { Minute = GameMinute, EventType = "Status", Description = "EXTRA TIME — 2nd Half begins!" });
    }

    /// <summary>Simulates a full penalty shootout immediately (for SkipToResult).</summary>
    public void ResolveShootout()
    {
        IsShootout = true;
        int home = RunShootoutRound();
        int away = RunShootoutRound();
        HomeShootoutScore = home;
        AwayShootoutScore = away;
        if (home == away) { HomeShootoutScore++; } // ensure a winner
    }

    private int RunShootoutRound()
    {
        // First 5 shots then sudden death up to 20
        int score = 0;
        for (int i = 0; i < 5; i++)
            if (_rng.NextDouble() < 0.75) score++;
        return score;
    }

    public void RequestTimeout(bool isHome)
    {
        var team = isHome ? HomeTeam : AwayTeam;
        int remaining = isHome ? HomeTimeoutsRemaining-- : AwayTimeoutsRemaining--;
        if (remaining > 0)
        {
            EventLog.Insert(0, new LiveMatchEvent { Minute = GameMinute, Second = (int)GameSecond, EventType = "Timeout", TeamId = team.Id, Description = $"{team.Name} called a timeout." });
            _currentAttackTime = 0; // Reset attack progress on timeout
        }
    }

    public void StartSecondHalf() 
    { 
        IsHalfTime = false; 
        ResetRequiredAttackTime(); 
        // Half-time replenishment (15%)
        foreach (var id in PlayerEnergy.Keys.ToList())
        {
            PlayerEnergy[id] = Math.Min(100.0, PlayerEnergy[id] + 15.0);
        }
    }

    public void PerformSubstitution(bool isHome, Player? subOut, Player? subIn, string position)
    {
        if (subOut == null || subIn == null) return;
        var squad = isHome ? HomeSquad : AwaySquad;
        squad.StartingLineup[position] = subIn;

        squad.Substitutes.Remove(subIn);
        squad.Substitutes.Add(subOut);

        // Mark incoming sub as having played
        _playersOnCourt.Add(subIn.Id);
        if (!Stats.ContainsKey(subIn.Id))
            Stats[subIn.Id] = new MatchPlayerStat { PlayerId = subIn.Id, PlayerName = subIn.Name, TeamId = subIn.TeamId ?? 0 };

        TriggerEvent("Substitution", subIn, $"{subIn.Name} enters the game for {subOut.Name}.");
    }

    public void AutoSubstitutionForAI(Team team, SquadSelection squad)
    {
        var onCourt = squad.StartingLineup.Values.Where(p => p != null).Distinct().ToList();
        foreach (var p in onCourt)
        {
            if (p == null) continue;
            // AI sub threshold (40% is more realistic for an AI swap)
            if (p.MatchEnergy < 40.0)
            {
                var sub = squad.Substitutes.OrderByDescending(s => s.MatchEnergy).FirstOrDefault(s => s.Position == p.Position && s.MatchEnergy > 75.0);
                if (sub != null)
                {
                    PerformSubstitution(team.Id == HomeTeam.Id, p, sub, p.Position);
                }
            }
        }
    }

    private void UpdateEnergyFromMinutePassed()
    {
        var courtPlayers = HomeSquad.StartingLineup.Values
            .Concat(AwaySquad.StartingLineup.Values)
            .Where(p => p != null)
            .Select(p => p!.Id)
            .Distinct()
            .ToHashSet();

        foreach (var p in HomeTeam.Players.Concat(AwayTeam.Players))
        {
            if (courtPlayers.Contains(p.Id))
            {
                // Active player fatigue (increased drain for realism: ~1.65x previous rate)
                p.MatchEnergy = Math.Max(0, p.MatchEnergy - (22 - p.Stamina) * 0.55);
            }
            else
            {
                // Bench player recovery (3% per min)
                p.MatchEnergy = Math.Min(100.0, p.MatchEnergy + 3.0);
            }
            PlayerEnergy[p.Id] = p.MatchEnergy;
        }
    }
}
