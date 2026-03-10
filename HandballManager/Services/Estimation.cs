namespace HandballManager.Services;

public static class Estimation
{
    public static string Range(int playerId, string key, int actual, int min, int max, int maxUncertainty)
    {
        if (min >= max) return actual.ToString();

        actual = Math.Clamp(actual, min, max);

        // Deterministic random for stable UI estimates.
        int seed = HashCode.Combine(playerId, key);
        var rng = new Random(seed);

        int uncertainty = Math.Clamp(rng.Next(maxUncertainty - 2, maxUncertainty + 1), 1, max - min);

        int lowSlack = rng.Next(1, uncertainty + 1);
        int highSlack = rng.Next(1, uncertainty + 1);

        int low = Math.Max(min, actual - lowSlack);
        int high = Math.Min(max, actual + highSlack);

        // Ensure the true value lies within the estimate.
        if (low > actual) low = actual;
        if (high < actual) high = actual;

        // Ensure a minimum visible window.
        if (high - low < 3)
        {
            int expand = 3 - (high - low);
            low = Math.Max(min, low - (expand / 2 + 1));
            high = Math.Min(max, high + (expand / 2 + 1));
        }

        if (low == high) return low.ToString();
        return $"{low}-{high}";
    }

    public static string EuroRange(int playerId, string key, decimal actual, decimal min, decimal max, decimal maxUncertainty)
    {
        actual = Math.Clamp(actual, min, max);
        int seed = HashCode.Combine(playerId, key);
        var rng = new Random(seed);

        decimal uncertainty = rng.Next((int)(maxUncertainty * 0.6m), (int)maxUncertainty + 1);

        decimal low = Math.Max(min, actual - rng.Next(1, (int)uncertainty + 1));
        decimal high = Math.Min(max, actual + rng.Next(1, (int)uncertainty + 1));

        // Round to nearest 1k for readability.
        low = Math.Round(low / 1000m) * 1000m;
        high = Math.Round(high / 1000m) * 1000m;

        if (low > actual) low = actual;
        if (high < actual) high = actual;

        if (low == high) return $"{low:N0} €";
        return $"{low:N0}–{high:N0} €";
    }
}

