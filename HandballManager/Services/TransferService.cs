using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class TransferService
{
    private readonly HandballDbContext _db;
    private static readonly Random Rng = new();
    private const int BaselineWorldTeams = 12; // Original single-league baseline (Romania-only)

    /// <summary>
    /// Set this to false to disable all automated AI transfer activity (offers and AI-to-AI transfers).
    /// </summary>
    public static bool TransfersEnabled { get; set; } = true;

    public TransferService(HandballDbContext db)
    {
        _db = db;
    }

    private async Task<double> GetWorldScaleFactorAsync()
    {
        int teams = await _db.Teams.CountAsync();
        if (teams <= 0) return 1.0;
        return Math.Max(1.0, teams / (double)BaselineWorldTeams);
    }

    /// <summary>With small probability during transfer window, create an AI offer for a random user player.</summary>
    public async Task TryGenerateAiOfferAsync(DateTime gameDate)
    {
        if (!TransfersEnabled || !LeagueService.IsWithinAnyTransferWindow(gameDate)) return;

        double scale = await GetWorldScaleFactorAsync();
        int chancePct = (int)Math.Round(Math.Clamp(4 * scale, 4, 15)); // scale with world size, cap to avoid spam
        if (Rng.Next(100) >= chancePct) return;

        var userTeam = await _db.Teams.Include(t => t.Players).FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (userTeam == null || userTeam.Players.Count == 0) return;

        // Avoid overwhelming the user with too many simultaneous offers
        int maxPending = (int)Math.Round(Math.Clamp(4 * scale, 4, 12));
        int pendingNow = await _db.TransferOffers
            .Include(o => o.ForPlayer)
            .CountAsync(o => o.Status == "Pending" && o.ForPlayer != null && o.ForPlayer.TeamId == userTeam.Id);
        if (pendingNow >= maxPending) return;

        var player = userTeam!.Players[Rng.Next(userTeam.Players.Count)];
        var aiTeams = await _db.Teams.Where(t => !t.IsPlayerTeam).Select(t => t.Id).ToListAsync();
        if (aiTeams.Count == 0) return;
        int fromTeamId = aiTeams[Rng.Next(aiTeams.Count)];
        bool freeContract = player.HasSixMonthsOrLessOnContract(gameDate);
        decimal wage = EstimateRequestedMonthlyWage(player) * (decimal)(0.8 + Rng.NextDouble() * 0.4);
        int years = Rng.Next(1, 4);
        _db.TransferOffers.Add(new TransferOffer
        {
            FromTeamId = fromTeamId,
            ForPlayerId = player.Id,
            OfferType = freeContract ? "FreeContract" : "Buyout",
            OfferAmount = freeContract ? 0 : player.BuyoutFee,
            ProposedMonthlyWage = Math.Round(wage, 0),
            ProposedContractYears = years,
            OfferedAt = gameDate,
            Status = "Pending"
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Each day during a transfer window, a small chance that one AI club
    /// buys a player from another AI club, generating a news item.
    /// Runs 1–3 transfers per day when triggered.
    /// </summary>
    public async Task TryGenerateAiToAiTransfersAsync(DateTime gameDate)
    {
        if (!TransfersEnabled || !LeagueService.IsWithinAnyTransferWindow(gameDate)) return;

        double scale = await GetWorldScaleFactorAsync();

        // Scale chance per day that any AI activity happens at all
        int triggerPct = (int)Math.Round(Math.Clamp(20 * scale, 20, 80));
        if (Rng.Next(100) >= triggerPct) return;

        var aiTeams = await _db.Teams
            .Where(t => !t.IsPlayerTeam)
            .Include(t => t.Players)
            .ToListAsync();

        if (aiTeams.Count < 2) return;

        // Scale transfers per triggered day with world size
        int minPerDay = (int)Math.Round(Math.Clamp(1 * scale, 1, 6));
        int maxPerDay = (int)Math.Round(Math.Clamp(2 * scale, 2, 10));
        if (maxPerDay < minPerDay) maxPerDay = minPerDay;
        int count = Rng.Next(minPerDay, maxPerDay + 1);
        for (int i = 0; i < count; i++)
        {
            // Pick a selling team that has players
            var sellingCandidates = aiTeams.Where(t => t.Players.Any(p => !p.IsRetired)).ToList();
            if (sellingCandidates.Count == 0) break;

            var sellingTeam = sellingCandidates[Rng.Next(sellingCandidates.Count)];
            var availablePlayers = sellingTeam.Players.Where(p => !p.IsRetired).ToList();
            if (availablePlayers.Count == 0) continue;

            var player = availablePlayers[Rng.Next(availablePlayers.Count)];

            // Pick a buying team (different from the selling team)
            var buyingCandidates = aiTeams.Where(t => t.Id != sellingTeam.Id).ToList();
            if (buyingCandidates.Count == 0) continue;
            var buyingTeam = buyingCandidates[Rng.Next(buyingCandidates.Count)];

            bool isFree = player.HasSixMonthsOrLessOnContract(gameDate);
            decimal wage = EstimateRequestedMonthlyWage(player) * (decimal)(0.85 + Rng.NextDouble() * 0.3);
            int years = Rng.Next(1, 4);
            DateTime contractEnd = new DateTime(gameDate.Year + years, 6, 30);

            if (isFree && LeagueService.IsWithinWinterTransferWindow(gameDate))
            {
                // Pre-contract: player joins on July 1 (start of summer window)
                _db.PendingTransfers.Add(new PendingTransfer
                {
                    PlayerId = player.Id,
                    FromTeamId = sellingTeam.Id,
                    ToTeamId = buyingTeam.Id,
                    AgreedMonthlyWage = Math.Round(wage, 0),
                    ContractEndDate = contractEnd,
                    EffectiveDate = new DateTime(gameDate.Year, 7, 1),
                    TransferType = "FreeContract",
                    CreatedAt = gameDate
                });
                await _db.SaveChangesAsync();
            }
            else
            {
                await ExecuteTransferInternalAsync(player.Id, sellingTeam.Id, buyingTeam.Id,
                    Math.Round(wage, 0), contractEnd, !isFree, gameDate);
            }
        }
    }

    /// <summary>Next available shirt number (1–99) for the team.</summary>
    public async Task<int> GetNextAvailableShirtNumberAsync(int teamId)
    {
        var used = await _db.Players
            .Where(p => p.TeamId == teamId && !p.IsRetired)
            .Select(p => p.ShirtNumber)
            .ToListAsync();
        for (int n = 1; n <= 99; n++)
        {
            if (!used.Contains(n)) return n;
        }
        return 99;
    }

    /// <summary>Estimated monthly wage a player might ask for, based on overall and age.</summary>
    public static decimal EstimateRequestedMonthlyWage(Player p)
    {
        double ovr = p.Overall100;
        double ageFactor = p.Age is >= 24 and <= 30 ? 1.0 : p.Age < 24 ? 0.6 + (p.Age - 18) * 0.067 : 1.0 - (p.Age - 30) * 0.05;
        ageFactor = Math.Clamp(ageFactor, 0.3, 1.2);
        double baseWage = 500 + (ovr - 40) * 25 * ageFactor;
        return (decimal)Math.Round(Math.Max(200, baseWage), 0);
    }

    public bool CanApproachToSign(Player player, DateTime gameDate)
    {
        if (player.TeamId == null) return LeagueService.IsWithinAnyTransferWindow(gameDate);
        return player.HasSixMonthsOrLessOnContract(gameDate) && LeagueService.IsWithinAnyTransferWindow(gameDate);
    }

    public bool CanMakeOffer(Player player, DateTime gameDate)
    {
        return LeagueService.IsWithinAnyTransferWindow(gameDate);
    }

    /// <summary>
    /// Agrees a transfer: either executes immediately (if in window) or queues a PendingTransfer.
    /// For free agents (≤6 months), if we're in winter window the effective date is contract expiry (June 30).
    /// </summary>
    public async Task<(bool executedNow, string message)> AgreeTransferAsync(
        int playerId,
        int toTeamId,
        decimal agreedMonthlyWage,
        int contractYears,
        string transferType,
        DateTime gameDate)
    {
        var player = await _db.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null) return (false, "Player not found.");
        if (player.IsRetired) return (false, "Player is retired.");

        int fromTeamId = player.TeamId ?? 0;
        DateTime contractEnd = contractYears <= 0
            ? new DateTime(gameDate.Year + 1, 6, 30)
            : new DateTime(gameDate.Year + contractYears, 6, 30);

        DateTime effectiveDate;
        if (LeagueService.IsWithinAnyTransferWindow(gameDate))
        {
            effectiveDate = gameDate.Date;
        }
        else
        {
            if (transferType == "FreeContract" && player.HasSixMonthsOrLessOnContract(gameDate))
                effectiveDate = player.ContractEndDate.Date;
            else
                effectiveDate = GetNextTransferWindowStart(gameDate);
        }

        if (effectiveDate == gameDate.Date)
        {
            await ExecuteTransferInternalAsync(playerId, fromTeamId, toTeamId, agreedMonthlyWage, contractEnd, transferType == "Buyout", gameDate);
            return (true, "Transfer completed.");
        }

        _db.PendingTransfers.Add(new PendingTransfer
        {
            PlayerId = playerId,
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            AgreedMonthlyWage = agreedMonthlyWage,
            ContractEndDate = contractEnd,
            EffectiveDate = effectiveDate,
            TransferType = transferType,
            CreatedAt = gameDate
        });
        await _db.SaveChangesAsync();
        return (false, $"Transfer will complete on {effectiveDate:dd MMM yyyy}.");
    }

    private static DateTime GetNextTransferWindowStart(DateTime after)
    {
        var summer = new DateTime(after.Year, 7, 1);
        var winter = new DateTime(after.Year, 1, 1);
        if (after < winter) return winter;
        if (after < summer) return summer;
        return new DateTime(after.Year + 1, 1, 1);
    }

    /// <summary>Execute a transfer: move player, set wage/contract, assign shirt number. If isBuyout, move fee between clubs.</summary>
    public async Task ExecuteTransferInternalAsync(int playerId, int fromTeamId, int toTeamId, decimal monthlyWage, DateTime contractEndDate, bool isBuyout = true, DateTime? gameDate = null)
    {
        var player = await _db.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null) return;

        // Prevent a player from being transferred more than once per season
        if (player.TransferredThisSeason) return;

        var toTeam = await _db.Teams.FindAsync(toTeamId);
        if (toTeam == null) return;

        var fromTeam = fromTeamId != 0 ? await _db.Teams.FindAsync(fromTeamId) : null;

        decimal fee = isBuyout && fromTeamId != 0 ? player.BuyoutFee : 0m;
        int shirt = await GetNextAvailableShirtNumberAsync(toTeamId);

        string fromTeamName = fromTeam?.Name ?? "Free agent";
        string toTeamName = toTeam.Name;
        string playerName = player.Name;

        player.TeamId = toTeamId;
        player.MonthlyWage = monthlyWage;
        player.ContractEndDate = contractEndDate;
        player.ShirtNumber = shirt;
        player.TransferredThisSeason = true;

        if (fee > 0)
        {
            if (fromTeam != null)
                fromTeam.ClubBalance += fee;
            toTeam.ClubBalance -= fee;
        }

        // Generate transfer news
        string title = isBuyout && fee > 0
            ? $"{playerName} joins {toTeamName}"
            : $"{playerName} signs for {toTeamName} on free transfer";
        string body = isBuyout && fee > 0
            ? $"{playerName} has completed a move from {fromTeamName} to {toTeamName} for a fee of {fee:N0} €."
            : $"{playerName} has signed for {toTeamName} from {fromTeamName} on a free transfer.";

        _db.NewsItems.Add(new NewsItem
        {
            Title = title,
            Body = body,
            PublishedAt = gameDate ?? DateTime.Now,
            NewsType = "Transfer",
            IsRead = false
        });

        await _db.SaveChangesAsync();
    }

    public async Task ReleaseExpiredContractsAsync(DateTime gameDate)
    {
        var expired = await _db.Players
            .Where(p => !p.IsRetired && !p.IsRetiringAtEndOfSeason && p.TeamId != null && p.ContractEndDate.Date < gameDate.Date)
            .ToListAsync();

        foreach (var player in expired)
        {
            _db.NewsItems.Add(new NewsItem
            {
                Title = $"{player.Name} released as contract expires",
                Body = $"{player.Name}'s contract has expired and they are now a free agent.",
                PublishedAt = gameDate,
                NewsType = "Transfer",
                IsRead = false
            });
            player.TeamId = null;
            player.MonthlyWage = 0;
        }

        if (expired.Count > 0)
        {
            await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            await _db.SaveChangesAsync();
            await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
        }
    }

    /// <summary>Process all pending transfers whose effective date is on or before the given date.</summary>
    public async Task ProcessPendingTransfersAsync(DateTime gameDate)
    {
        var pending = await _db.PendingTransfers
            .Where(t => t.EffectiveDate.Date <= gameDate.Date)
            .ToListAsync();

        foreach (var t in pending)
        {
            await ExecuteTransferInternalAsync(t.PlayerId, t.FromTeamId, t.ToTeamId, t.AgreedMonthlyWage, t.ContractEndDate, t.TransferType == "Buyout", gameDate);
            _db.PendingTransfers.Remove(t);
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Accept an incoming offer: execute transfer with offer terms, or queue if free-contract in winter.</summary>
    public async Task AcceptOfferAsync(int offerId, DateTime gameDate)
    {
        var offer = await _db.TransferOffers
            .Include(o => o.ForPlayer)
            .Include(o => o.FromTeam)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.Status == "Pending");
        if (offer?.ForPlayer == null) return;

        var player = offer.ForPlayer;
        int fromTeamId = player.TeamId ?? 0;
        int toTeamId = offer.FromTeamId;
        DateTime contractEnd = new DateTime(LeagueService.CurrentSeasonYear + offer.ProposedContractYears, 6, 30);

        bool isFreeInWinter = offer.OfferType == "FreeContract"
                           && LeagueService.IsWithinWinterTransferWindow(gameDate);

        if (isFreeInWinter)
        {
            // Pre-contract agreed: player moves on July 1, stays at current club until then
            _db.PendingTransfers.Add(new PendingTransfer
            {
                PlayerId = player.Id,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                AgreedMonthlyWage = offer.ProposedMonthlyWage,
                ContractEndDate = contractEnd,
                EffectiveDate = new DateTime(gameDate.Year, 7, 1),
                TransferType = "FreeContract",
                CreatedAt = gameDate
            });
            offer.Status = "Accepted";
            await _db.SaveChangesAsync();
        }
        else
        {
            await ExecuteTransferInternalAsync(player.Id, fromTeamId, toTeamId,
                offer.ProposedMonthlyWage, contractEnd, offer.OfferType == "Buyout", gameDate);
            offer.Status = "Accepted";
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Reject an incoming offer.</summary>
    public async Task RejectOfferAsync(int offerId)
    {
        var offer = await _db.TransferOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.Status == "Pending");
        if (offer != null) { offer.Status = "Rejected"; await _db.SaveChangesAsync(); }
    }

    /// <summary>
    /// Expires any pending transfer offers that were made during a transfer window
    /// that has now closed. Called daily so offers disappear as soon as the window ends.
    /// Winter window: Jan 1–31. Summer window: Jul 1–Aug 30.
    /// An offer is stale if it was offered during a window that has since ended.
    /// </summary>
    public async Task ExpireStaleOffersAsync(DateTime gameDate)
    {
        var pending = await _db.TransferOffers
            .Where(o => o.Status == "Pending")
            .ToListAsync();

        foreach (var offer in pending)
        {
            // Only expire offers that were made during a window that is now closed
            bool offeredInWinter = IsWithinWinterWindow(offer.OfferedAt);
            bool offeredInSummer = IsWithinSummerWindow(offer.OfferedAt);

            bool windowStillOpen = (offeredInWinter && IsWithinWinterWindow(gameDate))
                                || (offeredInSummer && IsWithinSummerWindow(gameDate));

            if (!windowStillOpen && (offeredInWinter || offeredInSummer))
                offer.Status = "Expired";
        }

        await _db.SaveChangesAsync();
    }

    private static bool IsWithinWinterWindow(DateTime date)
        => date.Month == 1 && date.Day >= 1 && date.Day <= 31;

    private static bool IsWithinSummerWindow(DateTime date)
        => (date.Month == 7) || (date.Month == 8 && date.Day <= 30);

    /// <summary>Contract renewal: set new end date (June 30 of expiry year) and wage.</summary>
    public async Task RenewContractAsync(int playerId, int years, decimal newMonthlyWage, DateTime renewalDate)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null) return;
        int expiryYear = renewalDate.Year + Math.Max(1, Math.Min(5, years));
        player.ContractEndDate = new DateTime(expiryYear, 6, 30);
        player.MonthlyWage = newMonthlyWage;
        await _db.SaveChangesAsync();
    }
}