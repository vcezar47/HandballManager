namespace HandballManager.Helpers;

public static class ContractDisplayHelper
{
    /// <summary>Formats contract time remaining as e.g. "6m", "1y 2m", "2y 3m".</summary>
    public static string FormatContractTimeLeft(DateTime contractEndDate, DateTime asOfDate)
    {
        if (contractEndDate.Date <= asOfDate.Date)
            return "Expired";
        int months = (contractEndDate.Year - asOfDate.Year) * 12 + (contractEndDate.Month - asOfDate.Month);
        if (contractEndDate.Day < asOfDate.Day) months--;
        if (months <= 0) return "Expired";
        int years = months / 12;
        int m = months % 12;
        if (years == 0) return $"{m}m";
        if (m == 0) return $"{years}y";
        return $"{years}y {m}m";
    }
}
