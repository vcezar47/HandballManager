using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class FacilityService
{
    private readonly HandballDbContext _db;
    private const int UpgradeWeeks = 12;

    public FacilityService(HandballDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Starts an upgrade for the specified facility type on the player's team.
    /// Returns true on success, false if blocked (e.g. already upgrading, max level, insufficient funds).
    /// </summary>
    public async Task<(bool Success, string Message)> UpgradeFacilityAsync(int teamId, FacilityType facilityType, DateTime gameDate)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null) return (false, "Team not found.");

        // One upgrade at a time
        if (team.TrainingFacilityUpgradeCompleteDate != null || team.YouthFacilityUpgradeCompleteDate != null)
            return (false, "An upgrade is already in progress. Wait for it to complete.");

        int currentLevel;
        decimal cost;
        string facilityName;

        if (facilityType == FacilityType.Training)
        {
            currentLevel = team.TrainingFacilityLevel;
            cost = FacilityLevel.GetTrainingCost(currentLevel);
            facilityName = "Training Facilities";
        }
        else
        {
            currentLevel = team.YouthFacilityLevel;
            cost = FacilityLevel.GetYouthCost(currentLevel);
            facilityName = "Youth Academy";
        }

        if (currentLevel >= FacilityLevel.MaxLevel)
            return (false, $"{facilityName} are already at the maximum level.");

        if (cost <= 0)
            return (false, "Invalid upgrade cost.");

        // Budget deduction: balance first, then spill into transfer budget
        if (cost > team.ClubBalance)
            return (false, $"Insufficient club balance. Need {cost:N0} €, have {team.ClubBalance:N0} €.");

        team.ClubBalance -= cost;

        // If club balance minus the cost causes it to dip below wage reserves,
        // the transfer budget is eaten into
        decimal totalWageReserve = team.WageBudget * 52m;
        if (team.ClubBalance < totalWageReserve)
        {
            decimal shortage = totalWageReserve - team.ClubBalance;
            team.TransferBudget = Math.Max(0, team.TransferBudget - shortage);
        }

        // Re-derive transfer budget to stay consistent
        team.TransferBudget = Math.Max(0, team.ClubBalance - totalWageReserve);

        // Set completion date
        DateTime completionDate = gameDate.AddDays(UpgradeWeeks * 7);
        if (facilityType == FacilityType.Training)
            team.TrainingFacilityUpgradeCompleteDate = completionDate;
        else
            team.YouthFacilityUpgradeCompleteDate = completionDate;

        // Record transaction
        _db.Transactions.Add(new Transaction
        {
            TeamId = teamId,
            Amount = -cost,
            Description = $"{facilityName} upgrade to {FacilityLevel.GetLabel(currentLevel + 1)}",
            Date = gameDate,
            Type = "FacilityUpgrade"
        });

        // News for player team
        if (team.IsPlayerTeam)
        {
            string nextLabel = FacilityLevel.GetLabel(currentLevel + 1);
            _db.NewsItems.Add(new NewsItem
            {
                Title = $"{facilityName} Upgrade Started",
                Body = $"Construction has begun to upgrade {facilityName.ToLower()} to \"{nextLabel}\". Expected completion: {completionDate:d MMM yyyy}. Cost: {cost:N0} €.",
                PublishedAt = gameDate,
                NewsType = "Finance"
            });
        }

        await _db.SaveChangesAsync();
        return (true, $"{facilityName} upgrade started! Completion: {completionDate:d MMM yyyy}.");
    }

    /// <summary>
    /// Called daily from ProcessDailyProgressionAsync. Checks all teams for completed facility upgrades.
    /// </summary>
    public async Task ProcessFacilityCompletionsAsync(DateTime currentDate)
    {
        var teams = await _db.Teams
            .Where(t => t.TrainingFacilityUpgradeCompleteDate != null || t.YouthFacilityUpgradeCompleteDate != null)
            .ToListAsync();

        foreach (var team in teams)
        {
            if (team.TrainingFacilityUpgradeCompleteDate != null && currentDate >= team.TrainingFacilityUpgradeCompleteDate)
            {
                team.TrainingFacilityLevel = Math.Min(team.TrainingFacilityLevel + 1, FacilityLevel.MaxLevel);
                team.TrainingFacilityUpgradeCompleteDate = null;

                if (team.IsPlayerTeam)
                {
                    _db.NewsItems.Add(new NewsItem
                    {
                        Title = "Training Facilities Upgraded!",
                        Body = $"The training facilities upgrade is complete! New level: \"{FacilityLevel.GetLabel(team.TrainingFacilityLevel)}\".",
                        PublishedAt = currentDate,
                        NewsType = "Finance"
                    });
                }
            }

            if (team.YouthFacilityUpgradeCompleteDate != null && currentDate >= team.YouthFacilityUpgradeCompleteDate)
            {
                team.YouthFacilityLevel = Math.Min(team.YouthFacilityLevel + 1, FacilityLevel.MaxLevel);
                team.YouthFacilityUpgradeCompleteDate = null;

                if (team.IsPlayerTeam)
                {
                    _db.NewsItems.Add(new NewsItem
                    {
                        Title = "Youth Academy Upgraded!",
                        Body = $"The youth academy upgrade is complete! New level: \"{FacilityLevel.GetLabel(team.YouthFacilityLevel)}\".",
                        PublishedAt = currentDate,
                        NewsType = "Finance"
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
    }
}

public enum FacilityType
{
    Training,
    Youth
}
