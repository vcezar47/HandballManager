using System.Text.Json;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class YouthIntakeService
{
    private readonly HandballDbContext _db;
    private static readonly Random Rng = new();
    private static readonly string[] FirstNames = ["Elena", "Maria", "Ioana", "Andreea", "Alexandra", "Diana", "Ana", "Cristina", "Laura", "Mihaela"];
    private static readonly string[] LastNames = ["Popescu", "Ionescu", "Marinescu", "Stan", "Dumitrescu", "Georgescu", "Constantinescu", "Radu", "Nistor", "Munteanu"];
    private static readonly string[] Positions = ["GK", "LW", "RW", "LB", "RB", "CB", "Pivot"];

    public YouthIntakeService(HandballDbContext db)
    {
        _db = db;
    }

    /// <summary>If date is March 20 and intake not yet generated for this year, generate 4-7 youth players per club.</summary>
    public async Task GenerateIntakeForDateIfNeededAsync(DateTime date)
    {
        if (date.Month != 3 || date.Day != 20) return;
        int year = date.Year;
        bool already = await _db.YouthIntakePlayers.AnyAsync(y => y.IntakeYear == year);
        if (already) return;

        var teams = await _db.Teams.ToListAsync();
        foreach (var team in teams)
        {
            int count = Rng.Next(4, 8);
            var usedShirts = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int shirt = NextShirt(usedShirts);
                usedShirts.Add(shirt);
                var youth = GenerateOneYouth(team.Id, year, shirt);
                _db.YouthIntakePlayers.Add(youth);
            }
        }
        await _db.SaveChangesAsync();
    }

    private static int NextShirt(HashSet<int> used)
    {
        for (int n = 1; n <= 99; n++)
        {
            if (!used.Contains(n)) return n;
        }
        return 99;
    }

    private static YouthIntakePlayer GenerateOneYouth(int clubId, int intakeYear, int suggestedShirt)
    {
        string first = FirstNames[Rng.Next(FirstNames.Length)];
        string last = LastNames[Rng.Next(LastNames.Length)];
        int age = Rng.Next(15, 18);
        var birthdate = new DateTime(intakeYear - age, Rng.Next(1, 13), Rng.Next(1, 28));
        string position = Positions[Rng.Next(Positions.Length)];

        var p = new Player
        {
            FirstName = first,
            LastName = last,
            Birthdate = birthdate,
            Position = position,
            Nationality = "ROU",
            Height = Rng.Next(165, 188),
            Weight = Rng.Next(55, 82),
            ShirtNumber = suggestedShirt,
            ContractEndDate = default,
            MonthlyWage = 0,
            TeamId = 0
        };
        SetRandomAttributes(p, position);

        var json = JsonSerializer.Serialize(PlayerSnapshot.FromPlayer(p));
        return new YouthIntakePlayer
        {
            ClubId = clubId,
            IntakeYear = intakeYear,
            FirstName = first,
            LastName = last,
            Birthdate = birthdate,
            Position = position,
            Nationality = "ROU",
            Height = p.Height,
            Weight = p.Weight,
            PlayerDataJson = json,
            SuggestedShirtNumber = suggestedShirt
        };
    }

    private static void SetRandomAttributes(Player p, string position)
    {
        int baseLevel = Rng.Next(3, 8);
        int v() => Math.Clamp(baseLevel + Rng.Next(-1, 3), 1, 20);
        p.Dribbling = v(); p.Finishing = v(); p.Marking = v(); p.Passing = v(); p.Technique = v();
        p.Aggression = v(); p.Anticipation = v(); p.Decisions = v(); p.Acceleration = v(); p.Pace = v(); p.Stamina = v(); p.Strength = v();
        p.Receiving = v(); p.LongThrows = v(); p.SevenMeterTaking = v(); p.Tackling = v();
        p.Reflexes = v(); p.Handling = v(); p.OneOnOnes = v(); p.Positioning = v();
        p.AerialReach = v(); p.Communication = v(); p.Eccentricity = v(); p.Throwing = v();
        p.Composure = v(); p.Concentration = v(); p.Determination = v(); p.Flair = v();
        p.Leadership = v(); p.OffTheBall = v(); p.Teamwork = v(); p.Vision = v();
        p.Balance = v(); p.JumpingReach = v(); p.NaturalFitness = v(); p.Agility = v();
        if (position == "GK")
        {
            p.Reflexes = Math.Clamp(p.Reflexes + Rng.Next(0, 3), 1, 20);
            p.Handling = Math.Clamp(p.Handling + Rng.Next(0, 2), 1, 20);
        }
    }

    /// <summary>Sign a youth player to the first team: create Player, assign shirt number, remove from intake.</summary>
    public async Task<Player?> SignYouthAsync(int youthIntakePlayerId, int clubId, decimal monthlyWage, int contractYears, DateTime gameDate)
    {
        var youth = await _db.YouthIntakePlayers.FirstOrDefaultAsync(y => y.Id == youthIntakePlayerId && y.ClubId == clubId);
        if (youth == null) return null;

        var snapshot = JsonSerializer.Deserialize<PlayerSnapshot>(youth.PlayerDataJson);
        if (snapshot == null) return null;

        int shirt = await new TransferService(_db).GetNextAvailableShirtNumberAsync(clubId);
        var player = snapshot.ToPlayer(clubId, shirt, monthlyWage, new DateTime(gameDate.Year + contractYears, 6, 30));
        _db.Players.Add(player);
        _db.YouthIntakePlayers.Remove(youth);
        await _db.SaveChangesAsync();
        return player;
    }

    /// <summary>
    /// On March 21, AI clubs automatically sign 2-4 of their youth intake players.
    /// Only runs once per year (checks that intake exists and signings haven't happened yet).
    /// </summary>
    public async Task SignAiYouthPlayersAsync(DateTime date)
    {
        if (date.Month != 3 || date.Day != 21) return;

        int year = date.Year;
        var aiTeams = await _db.Teams.Where(t => !t.IsPlayerTeam).Select(t => t.Id).ToListAsync();

        foreach (int teamId in aiTeams)
        {
            var intake = await _db.YouthIntakePlayers
                .Where(y => y.ClubId == teamId && y.IntakeYear == year)
                .ToListAsync();

            if (intake.Count == 0) continue;

            // Sign 2-4 players, or all if fewer are available
            int toSign = Math.Min(intake.Count, Rng.Next(2, 5));
            var shuffled = intake.OrderBy(_ => Rng.Next()).Take(toSign).ToList();

            foreach (var youth in shuffled)
            {
                var snapshot = JsonSerializer.Deserialize<PlayerSnapshot>(youth.PlayerDataJson);
                if (snapshot == null) continue;

                int shirt = await new TransferService(_db).GetNextAvailableShirtNumberAsync(teamId);
                decimal wage = Rng.Next(200, 800); // youth wages are low
                int contractYears = Rng.Next(2, 4);
                var player = snapshot.ToPlayer(teamId, shirt, wage, new DateTime(date.Year + contractYears, 6, 30));

                _db.Players.Add(player);
                _db.YouthIntakePlayers.Remove(youth);
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>DTO for serializing a player snapshot (youth intake).</summary>
    public class PlayerSnapshot
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime Birthdate { get; set; }
        public string Position { get; set; } = "";
        public string Nationality { get; set; } = "ROU";
        public int Height { get; set; }
        public int Weight { get; set; }
        public int Dribbling { get; set; }
        public int Finishing { get; set; }
        public int Marking { get; set; }
        public int Passing { get; set; }
        public int Technique { get; set; }
        public int Aggression { get; set; }
        public int Anticipation { get; set; }
        public int Decisions { get; set; }
        public int Acceleration { get; set; }
        public int Pace { get; set; }
        public int Stamina { get; set; }
        public int Strength { get; set; }
        public int Reflexes { get; set; }
        public int Handling { get; set; }
        public int OneOnOnes { get; set; }
        public int Positioning { get; set; }
        public int LongThrows { get; set; }
        public int SevenMeterTaking { get; set; }
        public int Tackling { get; set; }
        public int Receiving { get; set; }
        public int AerialReach { get; set; }
        public int Communication { get; set; }
        public int Eccentricity { get; set; }
        public int Throwing { get; set; }
        public int Composure { get; set; }
        public int Concentration { get; set; }
        public int Determination { get; set; }
        public int Flair { get; set; }
        public int Leadership { get; set; }
        public int OffTheBall { get; set; }
        public int Teamwork { get; set; }
        public int Vision { get; set; }
        public int Balance { get; set; }
        public int JumpingReach { get; set; }
        public int NaturalFitness { get; set; }
        public int Agility { get; set; }

        public static PlayerSnapshot FromPlayer(Player p)
        {
            return new PlayerSnapshot
            {
                FirstName = p.FirstName,
                LastName = p.LastName,
                Birthdate = p.Birthdate,
                Position = p.Position,
                Nationality = p.Nationality,
                Height = p.Height,
                Weight = p.Weight,
                Dribbling = p.Dribbling,
                Finishing = p.Finishing,
                Marking = p.Marking,
                Passing = p.Passing,
                Technique = p.Technique,
                Aggression = p.Aggression,
                Anticipation = p.Anticipation,
                Decisions = p.Decisions,
                Acceleration = p.Acceleration,
                Pace = p.Pace,
                Stamina = p.Stamina,
                Strength = p.Strength,
                Reflexes = p.Reflexes,
                Handling = p.Handling,
                OneOnOnes = p.OneOnOnes,
                Positioning = p.Positioning,
                LongThrows = p.LongThrows,
                SevenMeterTaking = p.SevenMeterTaking,
                Tackling = p.Tackling,
                Receiving = p.Receiving,
                AerialReach = p.AerialReach,
                Communication = p.Communication,
                Eccentricity = p.Eccentricity,
                Throwing = p.Throwing,
                Composure = p.Composure,
                Concentration = p.Concentration,
                Determination = p.Determination,
                Flair = p.Flair,
                Leadership = p.Leadership,
                OffTheBall = p.OffTheBall,
                Teamwork = p.Teamwork,
                Vision = p.Vision,
                Balance = p.Balance,
                JumpingReach = p.JumpingReach,
                NaturalFitness = p.NaturalFitness,
                Agility = p.Agility
            };
        }

        public Player ToPlayer(int teamId, int shirtNumber, decimal monthlyWage, DateTime contractEndDate)
        {
            var p = new Player
            {
                TeamId = teamId,
                ShirtNumber = shirtNumber,
                FirstName = FirstName,
                LastName = LastName,
                Birthdate = Birthdate,
                Position = Position,
                Nationality = Nationality,
                Height = Height,
                Weight = Weight,
                MonthlyWage = monthlyWage,
                ContractEndDate = contractEndDate,
                Dribbling = Dribbling,
                Finishing = Finishing,
                Marking = Marking,
                Passing = Passing,
                Technique = Technique,
                Aggression = Aggression,
                Anticipation = Anticipation,
                Decisions = Decisions,
                Acceleration = Acceleration,
                Pace = Pace,
                Stamina = Stamina,
                Strength = Strength,
                Reflexes = Reflexes,
                Handling = Handling,
                OneOnOnes = OneOnOnes,
                Positioning = Positioning,
                LongThrows = LongThrows,
                SevenMeterTaking = SevenMeterTaking,
                Tackling = Tackling,
                Receiving = Receiving,
                AerialReach = AerialReach,
                Communication = Communication,
                Eccentricity = Eccentricity,
                Throwing = Throwing,
                Composure = Composure,
                Concentration = Concentration,
                Determination = Determination,
                Flair = Flair,
                Leadership = Leadership,
                OffTheBall = OffTheBall,
                Teamwork = Teamwork,
                Vision = Vision,
                Balance = Balance,
                JumpingReach = JumpingReach,
                NaturalFitness = NaturalFitness,
                Agility = Agility,
                GrowthAccumulatorsJson = "{}",
                SeasonAttributeChangesJson = "{}"
            };
            return p;
        }
    }

    /// <summary>
    /// Removes unsigned youth candidates after March 30 or at season end.
    /// </summary>
    public async Task RemoveStaleIntakeAsync(DateTime date)
    {
        int year = date.Month < 6 ? date.Year : date.Year + 1; // logical season end is June
        var stale = await _db.YouthIntakePlayers
            .Where(y => y.IntakeYear < year || (y.IntakeYear == date.Year && date.Month == 3 && date.Day > 30) || (y.IntakeYear == date.Year && date.Month > 3))
            .ToListAsync();
        
        if (stale.Any())
        {
            _db.YouthIntakePlayers.RemoveRange(stale);
            await _db.SaveChangesAsync();
        }
    }
}