using HandballManager.Helpers;
using Xunit;

namespace HandballManager.Tests;

/// <summary>
/// The deterministic maths: as accurate as the system's (to a few units in the last place), and
/// pinned, so any change to it shows here before a phone and the server disagree.
/// </summary>
public class DetMathTests
{
    private static IEnumerable<double> Inputs(double from, double to, int n)
    {
        for (int i = 0; i < n; i++) yield return from + (to - from) * i / (n - 1);
    }

    private static void Close(double expected, double actual, double tolerance, string what)
    {
        double scale = Math.Max(1.0, Math.Abs(expected));
        Assert.True(Math.Abs(expected - actual) <= tolerance * scale, $"{what}: expected {expected:R}, got {actual:R}");
    }

    [Fact]
    public void SineAndCosineMatchTheSystemOnes()
    {
        foreach (double x in Inputs(-200, 200, 40001))
        {
            Close(Math.Sin(x), DetMath.Sin(x), 1e-14, $"sin {x}");
            Close(Math.Cos(x), DetMath.Cos(x), 1e-14, $"cos {x}");
        }
        Assert.Equal(0.0, DetMath.Sin(0.0));
        Assert.Equal(1.0, DetMath.Cos(0.0));
    }

    [Fact]
    public void ExpAndLogMatchTheSystemOnes()
    {
        foreach (double x in Inputs(-700, 700, 20001))
        {
            double e = Math.Exp(x);
            Assert.True(Math.Abs(e - DetMath.Exp(x)) <= 4e-15 * e, $"exp {x}: {e:R} vs {DetMath.Exp(x):R}");
        }
        foreach (double x in Inputs(1e-9, 1e6, 20001).Concat(Inputs(1e-300, 1e-290, 101)))
            Close(Math.Log(x), DetMath.Log(x), 1e-15, $"log {x}");

        Assert.Equal(0.0, DetMath.Log(1.0));
        Assert.Equal(1.0, DetMath.Exp(0.0));
        Assert.True(double.IsNegativeInfinity(DetMath.Log(0.0)));
        Assert.True(double.IsNaN(DetMath.Log(-1.0)));
    }

    [Fact]
    public void ArctangentPowerAndTanhMatchTheSystemOnes()
    {
        foreach (double y in Inputs(-50, 50, 401))
        foreach (double x in Inputs(-50, 50, 41))
            Close(Math.Atan2(y, x), DetMath.Atan2(y, x), 1e-14, $"atan2 {y},{x}");

        foreach (double x in Inputs(0.001, 200, 2001))
        foreach (double p in new[] { -2.5, -1, 0.37, 0.5, 1.7, 2, 3 })
        {
            double expected = Math.Pow(x, p);
            Assert.True(Math.Abs(expected - DetMath.Pow(x, p)) <= 1e-13 * Math.Max(1, expected), $"pow {x},{p}");
        }
        Assert.Equal(-8.0, DetMath.Pow(-2.0, 3.0), 12);
        Assert.Equal(4.0, DetMath.Pow(-2.0, 2.0), 12);

        foreach (double x in Inputs(-30, 30, 6001))
            Close(Math.Tanh(x), DetMath.Tanh(x), 1e-14, $"tanh {x}");
    }

    /// <summary>
    /// The exact bits, hashed. This number is what every device must reproduce; it changes only
    /// when DetMath is changed on purpose, and then this test is updated with it.
    /// </summary>
    [Fact]
    public void TheBitsArePinned()
    {
        ulong h = 14695981039346656037UL;
        void Add(double v)
        {
            ulong b = (ulong)BitConverter.DoubleToInt64Bits(v);
            for (int i = 0; i < 8; i++) { h ^= (byte)(b >> (8 * i)); h *= 1099511628211UL; }
        }
        foreach (double x in Inputs(-123.4, 123.4, 10001))
        {
            Add(DetMath.Sin(x)); Add(DetMath.Cos(x)); Add(DetMath.Exp(x / 25)); Add(DetMath.Log(Math.Abs(x) + 0.001));
            Add(DetMath.Atan2(x, 7.5)); Add(DetMath.Pow(Math.Abs(x) + 1, 0.37)); Add(DetMath.Tanh(x / 10));
        }
        Assert.Equal(DetMathPin.Hash, h.ToString("X16"));
    }
}

/// <summary>Kept apart so the value is easy to find and to update deliberately.</summary>
internal static class DetMathPin
{
    public const string Hash = "6DA6471B541FAFD5";
}
