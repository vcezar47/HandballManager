namespace HandballManager.Models;

public enum CoachLicense
{
    Level1,
    Level2,
    Level3,
    EHFMaster,
    EHFPRO,
    IHFLicense
}

public static class CoachLicenseExtensions
{
    public static string DisplayName(this CoachLicense license) => license switch
    {
        CoachLicense.Level1 => "Level 1 License",
        CoachLicense.Level2 => "Level 2 License",
        CoachLicense.Level3 => "Level 3 License",
        CoachLicense.EHFMaster => "EHF Master",
        CoachLicense.EHFPRO => "EHF PRO",
        CoachLicense.IHFLicense => "IHF License",
        _ => license.ToString()
    };

    public static ReputationLevel GetReputation(this CoachLicense license) => license switch
    {
        CoachLicense.Level1 => ReputationLevel.Local,
        CoachLicense.Level2 => ReputationLevel.Regional,
        CoachLicense.Level3 => ReputationLevel.National,
        CoachLicense.EHFMaster => ReputationLevel.European,
        CoachLicense.EHFPRO => ReputationLevel.European,
        CoachLicense.IHFLicense => ReputationLevel.Global,
        _ => ReputationLevel.Local
    };

    public static int GetAttributePoints(this CoachLicense license) => license switch
    {
        CoachLicense.Level1 => 30,
        CoachLicense.Level2 => 40,
        CoachLicense.Level3 => 50,
        CoachLicense.EHFMaster => 60,
        CoachLicense.EHFPRO => 70,
        CoachLicense.IHFLicense => 80,
        _ => 30
    };
}
