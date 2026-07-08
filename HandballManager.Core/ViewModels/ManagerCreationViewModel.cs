using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using System.Linq;

namespace HandballManager.ViewModels;

public partial class LicenseOption : ObservableObject
{
    public CoachLicense License { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Points { get; set; }
    public string ReputationLabel { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class ManagerCreationViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Action<Manager> _onManagerCreated;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private DateTime _birthdate = new(1985, 1, 1);
    
    [ObservableProperty]
    private int _selectedDay = 1;

    [ObservableProperty]
    private int _selectedMonth = 1;

    [ObservableProperty]
    private int _selectedYear = 1985;

    // Days is dynamic: only shows valid days for the selected month/year
    public List<int> Days => Enumerable.Range(1, DateTime.DaysInMonth(SelectedYear, SelectedMonth)).ToList();
    public List<int> Months { get; } = Enumerable.Range(1, 12).ToList();
    public List<int> Years { get; } = Enumerable.Range(DateTime.Now.Year - 75, 60).Reverse().ToList();

    [ObservableProperty]
    private string _placeOfBirth = string.Empty;

    [ObservableProperty]
    private string _nationality = "Romania";

    [ObservableProperty]
    private LicenseOption? _selectedLicense;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingPoints))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _motivation = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingPoints))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _youthDevelopment = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingPoints))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _discipline = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingPoints))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _adaptability = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingPoints))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _timeoutTalks = 1;

    public int TotalPoints => SelectedLicense?.Points ?? 30;
    public int UsedPoints => (Motivation - 1) + (YouthDevelopment - 1) + (Discipline - 1) + (Adaptability - 1) + (TimeoutTalks - 1);
    public int RemainingPoints => TotalPoints - UsedPoints;

    public bool CanCreate => !string.IsNullOrWhiteSpace(FirstName)
                          && !string.IsNullOrWhiteSpace(LastName)
                          && !string.IsNullOrWhiteSpace(PlaceOfBirth)
                          && SelectedLicense != null
                          && RemainingPoints >= 0;

    public List<LicenseOption> LicenseOptions { get; } = new()
    {
        new() { License = CoachLicense.Level1, DisplayName = "Level 1 License", Points = 30, ReputationLabel = "Local" },
        new() { License = CoachLicense.Level2, DisplayName = "Level 2 License", Points = 40, ReputationLabel = "Regional" },
        new() { License = CoachLicense.Level3, DisplayName = "Level 3 License", Points = 50, ReputationLabel = "National" },
        new() { License = CoachLicense.EHFMaster, DisplayName = "EHF Master", Points = 60, ReputationLabel = "European" },
        new() { License = CoachLicense.EHFPRO, DisplayName = "EHF PRO", Points = 70, ReputationLabel = "European" },
        new() { License = CoachLicense.IHFLicense, DisplayName = "IHF License", Points = 80, ReputationLabel = "Global" },
    };

    public List<string> Nationalities { get; } = new()
    {
        "Romania", "Hungary", "Serbia", "Croatia", "France",
        "Denmark", "Norway", "Sweden", "Germany", "Spain",
        "Netherlands", "Poland", "Brazil", "Montenegro", "Slovenia",
        "North Macedonia", "Czech Republic", "Russia", "Portugal", "Austria"
    };

    public ManagerCreationViewModel(HandballDbContext db, Action<Manager> onManagerCreated)
    {
        _db = db;
        _onManagerCreated = onManagerCreated;
        SelectedLicense = LicenseOptions[0];
        UpdateBirthdate();
    }

    partial void OnSelectedLicenseChanged(LicenseOption? value)
    {
        // Update IsSelected property on options
        foreach (var opt in LicenseOptions)
        {
            opt.IsSelected = (opt == value);
        }

        // Reset attributes when license changes
        Motivation = 1;
        YouthDevelopment = 1;
        Discipline = 1;
        Adaptability = 1;
        TimeoutTalks = 1;

        OnPropertyChanged(nameof(TotalPoints));
        OnPropertyChanged(nameof(RemainingPoints));
        OnPropertyChanged(nameof(CanCreate));
    }

    [RelayCommand]
    private void SetLicense(LicenseOption license)
    {
        SelectedLicense = license;
    }

    [RelayCommand]
    private void AdjustAttribute(string attributeName)
    {
        // The parameter should be "Name:Delta", e.g. "Motivation:1" or "Motivation:-1"
        var parts = attributeName.Split(':');
        if (parts.Length != 2) return;
        
        string name = parts[0];
        if (!int.TryParse(parts[1], out int delta)) return;

        switch (name)
        {
            case "Motivation":
                if (CanAdjust(Motivation, delta)) Motivation += delta;
                break;
            case "YouthDevelopment":
                if (CanAdjust(YouthDevelopment, delta)) YouthDevelopment += delta;
                break;
            case "Discipline":
                if (CanAdjust(Discipline, delta)) Discipline += delta;
                break;
            case "Adaptability":
                if (CanAdjust(Adaptability, delta)) Adaptability += delta;
                break;
            case "TimeoutTalks":
                if (CanAdjust(TimeoutTalks, delta)) TimeoutTalks += delta;
                break;
        }
    }

    private bool CanAdjust(int currentVal, int delta)
    {
        int newVal = currentVal + delta;
        if (newVal < 0 || newVal > 20) return false;
        if (delta > 0 && RemainingPoints <= 0) return false;
        return true;
    }

    partial void OnFirstNameChanged(string value) => OnPropertyChanged(nameof(CanCreate));
    partial void OnLastNameChanged(string value) => OnPropertyChanged(nameof(CanCreate));
    partial void OnPlaceOfBirthChanged(string value) => OnPropertyChanged(nameof(CanCreate));

    partial void OnSelectedDayChanged(int value) => UpdateBirthdate();

    partial void OnSelectedMonthChanged(int value)
    {
        // Refresh the Days list so only valid days appear for the new month
        OnPropertyChanged(nameof(Days));
        // Clamp the selected day if it exceeds the new month's max days
        int max = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
        if (SelectedDay > max) SelectedDay = max;
        UpdateBirthdate();
    }

    partial void OnSelectedYearChanged(int value)
    {
        // Refresh the Days list (February differs in leap years)
        OnPropertyChanged(nameof(Days));
        int max = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
        if (SelectedDay > max) SelectedDay = max;
        UpdateBirthdate();
    }

    private void UpdateBirthdate()
    {
        try
        {
            int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
            int safeDay = Math.Min(SelectedDay, daysInMonth);
            Birthdate = new DateTime(SelectedYear, SelectedMonth, safeDay);
        }
        catch { }
    }

    private static string NationalityToCode(string nationality) => nationality switch
    {
        "Romania" => "ROU", "Hungary" => "HUN", "Serbia" => "SRB", "Croatia" => "HRV",
        "France" => "FRA", "Denmark" => "DEN", "Norway" => "NOR", "Sweden" => "SWE",
        "Germany" => "GER", "Spain" => "ESP", "Netherlands" => "NED", "Poland" => "POL",
        "Brazil" => "BRA", "Montenegro" => "MNE", "Slovenia" => "SVN",
        "North Macedonia" => "MKD", "Czech Republic" => "CZE", "Russia" => "RUS",
        "Portugal" => "POR", "Austria" => "AUT",
        _ => "ROU"
    };

    [RelayCommand]
    private void CreateManager()
    {
        if (!CanCreate || SelectedLicense == null) return;

        var manager = new Manager
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Birthdate = Birthdate,
            PlaceOfBirth = PlaceOfBirth.Trim(),
            Nationality = NationalityToCode(Nationality),
            License = SelectedLicense.License,
            Reputation = SelectedLicense.License.GetReputation(),
            Motivation = Motivation,
            YouthDevelopment = YouthDevelopment,
            Discipline = Discipline,
            Adaptability = Adaptability,
            TimeoutTalks = TimeoutTalks,
            IsPlayerManager = true,
            ClubHistoryJson = "[]"
        };

        _db.Managers.Add(manager);
        _db.SaveChanges();

        _onManagerCreated(manager);
    }
}
