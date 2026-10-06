using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>One player in a recorded frame.</summary>
public struct PlayerShot
{
    public int Id, Side, Number;
    public bool Keeper, Airborne, Down, HasBall;

    /// <summary>Off the court but in view: walking to the bench, or waiting at the line to come on.</summary>
    public bool Touchline;
    public Vector2 Pos;
    public AgentState State;
}

/// <summary>
/// The court as it stood after one tick: everything a renderer needs, and nothing it could use
/// to reach back into the match. Recorded every tick so a stretch can be played back — which is
/// how the 16× and 32× speeds stop to show the goals.
/// </summary>
public sealed class FrameShot
{
    public long Tick;
    public float Time;

    /// <summary>The match clock, which stands still for seven-metres; <see cref="Time"/> keeps the engine's pace.</summary>
    public float GameTime;
    public int HomeScore, AwayScore;
    public Vector2 Ball;
    public float BallHeight;
    public BallState BallState;
    public Vector2 Ref0, Ref1;
    public int Count;
    public PlayerShot[] Players = new PlayerShot[40];

    public void Capture(PhysicsMatch m, long tick)
    {
        Tick = tick;
        Time = m.Clock;
        GameTime = m.GameTime;
        HomeScore = m.Score[0];
        AwayScore = m.Score[1];
        Ball = m.Ball.Pos;
        BallHeight = m.Ball.Height;
        BallState = m.Ball.State;
        Ref0 = m.Referees[0].Pos;
        Ref1 = m.Referees[1].Pos;

        Count = Math.Min(m.Agents.Count, Players.Length);
        for (int i = 0; i < Count; i++)
        {
            var a = m.Agents[i];
            Players[i] = new PlayerShot
            {
                Id = a.Id,
                Side = a.Side,
                Number = a.Player.ShirtNumber,
                Keeper = a.IsKeeper,
                Airborne = a.Airborne,
                Down = a.State == AgentState.Injured && a.StunnedUntil > m.Clock,
                HasBall = m.Ball.Holder == a && m.Ball.State == Engine2D.BallState.Held,
                Pos = a.Pos,
                State = a.State,
                Touchline = a.OnTouchline,
            };
        }
    }
}
