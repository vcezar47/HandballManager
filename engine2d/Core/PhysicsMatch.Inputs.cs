namespace HandballManager.Engine2D;

public sealed partial class PhysicsMatch
{
    // ── The manager's inputs, recorded ─────────────────────────────────────────

    /// <summary>Ticks that moved the match: not those spent waiting at half-time or after the whistle.</summary>
    public long Steps { get; private set; }

    /// <summary>Everything the manager did, in order, each stamped with the step it was done at.</summary>
    public List<MatchInput> Inputs { get; } = [];

    /// <summary>
    /// Played out unseen after the squad screen's INSTANT SIM, which ran the classic engine to
    /// full time first. A replay does the same, so the run it checks is the one the phone made.
    /// </summary>
    public bool Instant { get; set; }

    /// <summary>
    /// Does what the manager asked and writes it down. The one way in for the bench: whatever is
    /// changed any other way is not in <see cref="Inputs"/> and a replay will not do it.
    /// </summary>
    /// <returns>Whether it took: a substitution can be refused, a time-out may only be queued.</returns>
    public bool Apply(MatchInput input)
    {
        int side = Setup.UserSide;
        if (side < 0 || IsFinished) return false;
        Inputs.Add(input with { Step = Steps });

        switch (input)
        {
            case TacticsInput t:
                var tactics = Sides[side].Tactics.Clone();
                tactics.Tempo = t.Tempo;
                tactics.Focus = t.Focus;
                tactics.System = t.System;
                tactics.Aggression = t.Aggression;
                tactics.UseEmptyGoal = t.EmptyGoal;
                tactics.PlayForTime = t.PlayForTime;
                SetTactics(side, tactics);
                return true;

            case TimeoutInput:
                return ToggleTimeoutRequest(side);

            case SubstituteInput sub:
                var off = Sides[side].Squad.FirstOrDefault(a => a.Id == sub.OutgoingId);
                var on = Sides[side].Squad.FirstOrDefault(a => a.Id == sub.IncomingId);
                return off != null && on != null && RequestSubstitution(side, off, on);

            case SevenMetreInput seven:
                return ChooseForSevenMetre(seven.PlayerId);

            case SecondHalfInput:
                if (!IsHalfTime) return false;
                StartSecondHalf();
                return true;

            case AskForSevenMetresInput ask:
                AskForSevenMetreTaker = ask.On;
                return true;

            case AutoSubstitutionsInput auto:
                UserAutoSubstitutions = auto.On;
                return true;

            case RunToEndInput:
                ChooseForSevenMetre(0);
                AskForSevenMetreTaker = false;
                RunToEnd();
                return true;

            default:
                throw new ArgumentOutOfRangeException(nameof(input), input.GetType().Name);
        }
    }

    /// <summary>Names the taker, or the keeper for a throw against; 0 for the usual one.</summary>
    private bool ChooseForSevenMetre(int playerId)
    {
        if (AwaitingKeeper)
        {
            ChooseSevenMetreKeeper(playerId);
            return true;
        }
        if (!AwaitingTaker) return false;
        // The usual taker is the best of those on court, as the panel lists them.
        int id = playerId != 0 ? playerId
            : Sides[Setup.UserSide].Court.Where(a => !a.IsKeeper).OrderByDescending(a => a.Player.SevenMeterTaking).FirstOrDefault()?.Id ?? 0;
        ChooseSevenMetreTaker(id);
        return true;
    }

    /// <summary>
    /// Plays a freshly built match to full time with the inputs a phone recorded, each at its step.
    /// </summary>
    /// <returns>Null when it played through; otherwise why the inputs cannot be what a phone did.</returns>
    public string? Replay(IReadOnlyList<MatchInput> inputs)
    {
        int next = 0;
        while (!IsFinished)
        {
            while (next < inputs.Count && inputs[next].Step == Steps && !IsFinished)
                Apply(inputs[next++]);
            if (IsFinished) break;
            if (next < inputs.Count && inputs[next].Step < Steps)
                return $"a match input out of order at step {inputs[next].Step}";

            // The screen never moves the match on while it waits for the manager.
            if (IsHalfTime) return $"the match waits at half-time (step {Steps}) and nobody started the second half";
            if (AwaitingChoice) return $"the match waits for a seven-metre choice (step {Steps}) that never came";
            Tick();
        }
        return next < inputs.Count ? $"{inputs.Count - next} match inputs after the final whistle" : null;
    }
}
