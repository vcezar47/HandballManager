using HandballManager.Services;

namespace HandballManager.Services;

public sealed class ScoutingService
{
    private readonly GameClock _clock;

    public event EventHandler? StateChanged;

    private readonly List<ScoutingAssignment> _activeAssignments = new();

    private readonly HashSet<int> _scoutedPlayerIds = new();
    private readonly HashSet<int> _shortlistPlayerIds = new();

    public IReadOnlyCollection<int> ScoutedPlayerIds => _scoutedPlayerIds;
    public IReadOnlyCollection<int> ShortlistPlayerIds => _shortlistPlayerIds;
    public IReadOnlyCollection<ScoutingAssignment> ActiveAssignments => _activeAssignments;

    public int MaxConcurrentAssignments { get; } = 5;

    public ScoutingService(GameClock clock)
    {
        _clock = clock;
        _clock.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameClock.CurrentDate))
                OnDateChanged(_clock.CurrentDate);
        };
    }

    public bool IsScouted(int playerId) => _scoutedPlayerIds.Contains(playerId);

    public bool CanStartScouting(int playerId)
        => _activeAssignments.Count < MaxConcurrentAssignments
           && !_scoutedPlayerIds.Contains(playerId)
           && !_activeAssignments.Any(a => a.PlayerId == playerId);

    public bool TryStartScouting(int playerId)
    {
        if (!CanStartScouting(playerId))
            return false;

        var startedOn = _clock.CurrentDate.Date;
        var assignment = new ScoutingAssignment(
            PlayerId: playerId,
            StartedOn: startedOn,
            CompletesOn: startedOn.AddDays(14)
        );
        _activeAssignments.Add(assignment);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void CancelAllScouting()
    {
        if (_activeAssignments.Count == 0) return;
        _activeAssignments.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddToShortlist(int playerId)
    {
        if (_shortlistPlayerIds.Add(playerId))
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveFromShortlist(int playerId)
    {
        if (_shortlistPlayerIds.Remove(playerId))
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDateChanged(DateTime currentDate)
    {
        if (_activeAssignments.Count == 0) return;

        var completed = _activeAssignments
            .Where(a => currentDate.Date >= a.CompletesOn.Date)
            .ToList();

        if (completed.Count == 0) return;

        foreach (var a in completed)
        {
            _scoutedPlayerIds.Add(a.PlayerId);
            _activeAssignments.Remove(a);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ScoutingAssignment(int PlayerId, DateTime StartedOn, DateTime CompletesOn)
{
    public int DaysRemaining(DateTime currentDate)
    {
        var remaining = (CompletesOn.Date - currentDate.Date).Days;
        return Math.Max(0, remaining);
    }
}

