namespace HandballManager.Helpers;

/// <summary>
/// Sine, cosine, exponential, logarithm, arctangent, power and tanh that give the same bits on
/// every device.
/// </summary>
/// <remarks>
/// <para>
/// The system versions call the platform's C library, which differs in the last bit between
/// Android, Windows and Linux. That is invisible to a player, but the 2D engine is a chaotic
/// system: one last-bit difference grows into a different match. A Weekly Challenge run is only
/// verifiable if the server, replaying it, computes exactly what the phone did, so the simulation
/// uses these instead.
/// </para>
/// <para>
/// Built from nothing but <c>+ - * /</c>, <see cref="Math.Floor"/>, <see cref="Math.Sqrt"/> and
/// <see cref="Math.ScaleB"/>, every one of which IEEE 754 or .NET pins to a single answer. The
/// polynomials are written out in Horner form so the order of operations is fixed. Accuracy is
/// within a few units in the last place of double, far finer than the game needs; agreement is
/// what matters. The float overloads compute in double and round once at the end.
/// </para>
/// <para>
/// The determinism probe (Diagnostics/DeterminismProbe) hashes these; a test pins that hash, so
/// a change here that moves any bit is caught on the PC before a phone disagrees.
/// </para>
/// </remarks>
public static class DetMath
{
    public const double Pi = 3.14159265358979311600e+00;
    private const double HalfPi = 1.57079632679489655800e+00;
    private const double TwoOverPi = 6.36619772367581382433e-01;

    // π/2 split in two (fdlibm's pio2_1 / pio2_1t): the first part has 33 significant bits, so
    // k·part is exact for any k the game produces, and the reduction loses nothing.
    private const double PiOver2Hi = 1.57079632673412561417e+00;
    private const double PiOver2Lo = 6.07710050650619224932e-11;

    private const double Ln2Hi = 6.93147180369123816490e-01;
    private const double Ln2Lo = 1.90821492927058770002e-10;
    private const double InvLn2 = 1.44269504088896338700e+00;
    private const double Sqrt2 = 1.41421356237309514547e+00;
    private const double Sqrt3 = 1.73205080756887719318e+00;
    private const double Tan15 = 2.67949192431122696e-01; // tan(π/12)
    private const double PiOver6 = 5.23598775598298815659e-01;

    // ─── Sine and cosine ───────────────────────────────────────────────────────

    public static double Sin(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
        var (r, q) = Reduce(x);
        return q switch
        {
            0 => SinKernel(r),
            1 => CosKernel(r),
            2 => -SinKernel(r),
            _ => -CosKernel(r),
        };
    }

    public static double Cos(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
        var (r, q) = Reduce(x);
        return q switch
        {
            0 => CosKernel(r),
            1 => -SinKernel(r),
            2 => -CosKernel(r),
            _ => SinKernel(r),
        };
    }

    /// <summary>x as r + q·π/2 with |r| ≤ π/4, q taken mod 4.</summary>
    private static (double R, int Q) Reduce(double x)
    {
        double k = Math.Floor(x * TwoOverPi + 0.5);
        double r = (x - k * PiOver2Hi) - k * PiOver2Lo;
        long kl = (long)k;
        return (r, (int)(((kl % 4) + 4) % 4));
    }

    /// <summary>Taylor series to r¹⁷ on |r| ≤ π/4; the first term left out is below 10⁻¹⁸.</summary>
    private static double SinKernel(double r)
    {
        double z = r * r;
        double p = z * (-1.0 / 6.0 + z * (1.0 / 120.0 + z * (-1.0 / 5040.0 + z * (1.0 / 362880.0
                 + z * (-1.0 / 39916800.0 + z * (1.0 / 6227020800.0 + z * (-1.0 / 1307674368000.0
                 + z * (1.0 / 355687428096000.0))))))));
        return r + r * p;
    }

    /// <summary>Taylor series to r¹⁸ on |r| ≤ π/4.</summary>
    private static double CosKernel(double r)
    {
        double z = r * r;
        double p = z * (-1.0 / 2.0 + z * (1.0 / 24.0 + z * (-1.0 / 720.0 + z * (1.0 / 40320.0
                 + z * (-1.0 / 3628800.0 + z * (1.0 / 479001600.0 + z * (-1.0 / 87178291200.0
                 + z * (1.0 / 20922789888000.0 + z * (-1.0 / 6402373705728000.0)))))))));
        return 1.0 + p;
    }

    // ─── Exponential and logarithm ─────────────────────────────────────────────

    public static double Exp(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x > 709.78) return double.PositiveInfinity;
        if (x < -745.13) return 0.0;

        // x = k·ln2 + r, |r| ≤ ln2/2, then eʳ by its Taylor series to r¹⁴ and 2ᵏ exactly.
        double k = Math.Floor(x * InvLn2 + 0.5);
        double r = (x - k * Ln2Hi) - k * Ln2Lo;
        double p = 1.0 + r * (1.0 + r * (1.0 / 2.0 + r * (1.0 / 6.0 + r * (1.0 / 24.0 + r * (1.0 / 120.0
                 + r * (1.0 / 720.0 + r * (1.0 / 5040.0 + r * (1.0 / 40320.0 + r * (1.0 / 362880.0
                 + r * (1.0 / 3628800.0 + r * (1.0 / 39916800.0 + r * (1.0 / 479001600.0
                 + r * (1.0 / 6227020800.0 + r * (1.0 / 87178291200.0))))))))))))));
        return Math.ScaleB(p, (int)k);
    }

    public static double Log(double x)
    {
        if (double.IsNaN(x) || x < 0) return double.NaN;
        if (x == 0) return double.NegativeInfinity;
        if (double.IsPositiveInfinity(x)) return x;

        int e = 0;
        if (x < 2.2250738585072014e-308) { x *= 18014398509481984.0; e = -54; } // subnormal: lift by 2⁵⁴

        // x = m·2ᵉ with m in [√½, √2), then log m = 2·atanh(s), s = (m−1)/(m+1), |s| ≤ 0.172.
        long bits = BitConverter.DoubleToInt64Bits(x);
        e += (int)((bits >> 52) & 0x7FF) - 1023;
        double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        if (m > Sqrt2) { m *= 0.5; e += 1; }

        double s = (m - 1.0) / (m + 1.0);
        double z = s * s;
        double t = z * (1.0 / 3.0 + z * (1.0 / 5.0 + z * (1.0 / 7.0 + z * (1.0 / 9.0 + z * (1.0 / 11.0
                 + z * (1.0 / 13.0 + z * (1.0 / 15.0 + z * (1.0 / 17.0 + z * (1.0 / 19.0
                 + z * (1.0 / 21.0 + z * (1.0 / 23.0 + z * (1.0 / 25.0))))))))))));
        double logM = 2.0 * (s + s * t);
        return e * Ln2Hi + (logM + e * Ln2Lo);
    }

    /// <summary>xʸ. Negative bases only for whole-number powers, as the system version.</summary>
    public static double Pow(double x, double y)
    {
        if (y == 0) return 1.0;
        if (x == 1) return 1.0;
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        if (x == 0) return y > 0 ? 0.0 : double.PositiveInfinity;
        if (x > 0) return Exp(y * Log(x));

        if (Math.Floor(y) != y) return double.NaN;
        double magnitude = Exp(y * Log(-x));
        return Math.Floor(y * 0.5) * 2 == y ? magnitude : -magnitude;
    }

    public static double Tanh(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x > 20) return 1.0;
        if (x < -20) return -1.0;
        double e = Exp(2.0 * x);
        return (e - 1.0) / (e + 1.0);
    }

    // ─── Arctangent ────────────────────────────────────────────────────────────

    public static double Atan(double x)
    {
        if (double.IsNaN(x)) return x;
        bool negative = x < 0;
        if (negative) x = -x;

        bool inverted = x > 1.0;
        if (inverted) x = 1.0 / x;

        // Into |x| ≤ tan 15°, where the series converges fast: atan x = π/6 + atan((x√3 − 1)/(x + √3)).
        bool shifted = x > Tan15;
        if (shifted) x = (x * Sqrt3 - 1.0) / (x + Sqrt3);

        double z = x * x;
        double p = z * (-1.0 / 3.0 + z * (1.0 / 5.0 + z * (-1.0 / 7.0 + z * (1.0 / 9.0 + z * (-1.0 / 11.0
                 + z * (1.0 / 13.0 + z * (-1.0 / 15.0 + z * (1.0 / 17.0 + z * (-1.0 / 19.0
                 + z * (1.0 / 21.0 + z * (-1.0 / 23.0 + z * (1.0 / 25.0))))))))))));
        double r = x + x * p;

        if (shifted) r += PiOver6;
        if (inverted) r = HalfPi - r;
        return negative ? -r : r;
    }

    public static double Atan2(double y, double x)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        if (x > 0) return Atan(y / x);
        if (x < 0) return y >= 0 ? Atan(y / x) + Pi : Atan(y / x) - Pi;
        return y > 0 ? HalfPi : y < 0 ? -HalfPi : 0.0;
    }

    // ─── Float overloads: computed in double, rounded once ─────────────────────

    public static float Sin(float x) => (float)Sin((double)x);
    public static float Cos(float x) => (float)Cos((double)x);
    public static float Exp(float x) => (float)Exp((double)x);
    public static float Log(float x) => (float)Log((double)x);
    public static float Atan2(float y, float x) => (float)Atan2((double)y, (double)x);
    public static float Pow(float x, float y) => (float)Pow((double)x, (double)y);
    public static float Tanh(float x) => (float)Tanh((double)x);
}
