using System.Text.Json.Serialization;
using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// One thing the manager did on the bench during a 2D match, and the step it was done at.
/// </summary>
/// <remarks>
/// <para>
/// The match only moves through <see cref="PhysicsMatch.Tick"/>, and everything the manager can
/// change goes through <see cref="PhysicsMatch.Apply"/> between two ticks. So the same setup, the
/// same seed and these inputs at the same steps play the same match again, frame for frame —
/// which is how a Weekly Challenge run's watched matches are checked on the server.
/// </para>
/// <para>
/// <see cref="Step"/> counts the ticks that moved the match (<see cref="PhysicsMatch.Steps"/>),
/// not the clock: a time-out's huddle stops the clock but not the match.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "k")]
[JsonDerivedType(typeof(TacticsInput), "tactics")]
[JsonDerivedType(typeof(TimeoutInput), "timeout")]
[JsonDerivedType(typeof(SubstituteInput), "sub")]
[JsonDerivedType(typeof(SevenMetreInput), "7m")]
[JsonDerivedType(typeof(SecondHalfInput), "second-half")]
[JsonDerivedType(typeof(AskForSevenMetresInput), "ask-7m")]
[JsonDerivedType(typeof(AutoSubstitutionsInput), "auto-subs")]
[JsonDerivedType(typeof(RunToEndInput), "to-end")]
public abstract record MatchInput
{
    public long Step { get; init; }
}

/// <summary>New instructions for the manager's side, from the next decision on.</summary>
public sealed record TacticsInput(AttackTempo Tempo, AttackFocus Focus, DefensiveSystem System, DefensiveAggression Aggression,
    bool EmptyGoal, bool PlayForTime) : MatchInput;

/// <summary>The green card: asks for a time-out, or withdraws one waiting for the ball.</summary>
public sealed record TimeoutInput : MatchInput;

public sealed record SubstituteInput(int OutgoingId, int IncomingId) : MatchInput;

/// <summary>The taker of a seven-metre, or the keeper to face one; 0 for the usual one.</summary>
public sealed record SevenMetreInput(int PlayerId) : MatchInput;

public sealed record SecondHalfInput : MatchInput;

public sealed record AskForSevenMetresInput(bool On) : MatchInput;

public sealed record AutoSubstitutionsInput(bool On) : MatchInput;

/// <summary>The rest of the match played without stopping: INSTANT SIM, or SKIP on the court.</summary>
public sealed record RunToEndInput : MatchInput;
