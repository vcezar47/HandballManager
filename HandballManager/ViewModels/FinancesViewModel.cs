using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using System;

namespace HandballManager.ViewModels;

public partial class FinancesViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private Team? _team;
    private decimal _originalTransferBudget;
    private decimal _originalWageBudget;
    private decimal _totalBudgetBase; // Baseline to calculate the max/min amounts

    [ObservableProperty]
    private decimal _transferBudget;

    [ObservableProperty]
    private decimal _wageBudget;

    [ObservableProperty]
    private decimal _clubBalance;

    // Minimums so the team doesn't go entirely bankrupt in one area
    private readonly decimal _minTransferBudget = 10000;
    private readonly decimal _minWageBudget = 1000;

    // Slider bounds
    public double MinSliderValue => 0.0;
    public double MaxSliderValue => 100.0;

    // Default slider value is 50%, representing the current balance
    [ObservableProperty]
    private double _budgetSliderValue = 50.0;

    public FinancesViewModel(HandballDbContext db)
    {
        _db = db;
    }

    public void Initialize(Team team)
    {
        _team = team;
        _originalTransferBudget = team.TransferBudget;
        _originalWageBudget = team.WageBudget;

        // "Total budget" in transfer terms: Transfer Budget + (Wage Budget * 52)
        _totalBudgetBase = _originalTransferBudget + (_originalWageBudget * 52m);

        // Reset the view
        TransferBudget = _originalTransferBudget;
        WageBudget = _originalWageBudget;
        ClubBalance = _team.ClubBalance;
        BudgetSliderValue = 50.0; 
    }

    // When the slider changes, adjust the budgets
    partial void OnBudgetSliderValueChanged(double value)
    {
        if (_team == null || _totalBudgetBase == 0) return;

        // Value goes from 0 to 100
        // 50 means no change from initial loaded state
        // < 50 means shifting money to wage budget
        // > 50 means shifting money to transfer budget
        
        // Calculate the maximum we can move either way
        // Max we can shift to Wage Budget (value = 0) is bounded by MinTransferBudget
        decimal maxShiftToWage = _originalTransferBudget - _minTransferBudget;
        // Max we can shift to Transfer Budget (value = 100) is bounded by MinWageBudget (multiplied by 52 to make it comparable to transfer budget)
        decimal maxShiftToTransfer = (_originalWageBudget - _minWageBudget) * 52m;

        if (maxShiftToWage < 0) maxShiftToWage = 0;
        if (maxShiftToTransfer < 0) maxShiftToTransfer = 0;

        decimal newTransfer = _originalTransferBudget;
        decimal newWage = _originalWageBudget;

        if (value < 50)
        {
            // Shifting money to wage budget.
            // 0 - 50 scale. Ratio is (50 - value) / 50.
            double ratio = (50 - value) / 50.0;
            decimal shiftAmountInTransferTerms = (decimal)ratio * maxShiftToWage;
            
            newTransfer = _originalTransferBudget - shiftAmountInTransferTerms;
            newWage = _originalWageBudget + (shiftAmountInTransferTerms / 52m);
        }
        else if (value > 50)
        {
            // Shifting money to transfer budget
            // 50 - 100 scale. Ratio is (value - 50) / 50.
            double ratio = (value - 50) / 50.0;
            decimal shiftAmountInTransferTerms = (decimal)ratio * maxShiftToTransfer;

            newTransfer = _originalTransferBudget + shiftAmountInTransferTerms;
            newWage = _originalWageBudget - (shiftAmountInTransferTerms / 52m);
        }

        // Clamp just to be safe
        TransferBudget = Math.Max(_minTransferBudget, Math.Round(newTransfer, 2));
        WageBudget = Math.Max(_minWageBudget, Math.Round(newWage, 2));
    }

    [RelayCommand]
    private void ConfirmBudgets()
    {
        if (_team == null) return;

        // Only save to DB if changed
        if (TransferBudget != _originalTransferBudget || WageBudget != _originalWageBudget)
        {
            _team.TransferBudget = Math.Round(TransferBudget, 0); // we round to integers because it's cleaner
            _team.WageBudget = Math.Round(WageBudget, 0);
            
            _db.Teams.Update(_team);
            _db.SaveChanges();

            // Reset our tracking values so the "slider at 50%" meaning updates
            _originalTransferBudget = _team.TransferBudget;
            _originalWageBudget = _team.WageBudget;
            _totalBudgetBase = _originalTransferBudget + (_originalWageBudget * 52m);

            // Trigger re-calc of slider to center it without changing values
            BudgetSliderValue = 50.0;
            OnPropertyChanged(nameof(BudgetSliderValue));
        }
    }
}
