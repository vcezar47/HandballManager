using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Microsoft.EntityFrameworkCore;
using HandballManager.Data;
using HandballManager.Models;

namespace HandballManager.Data;

public static class DatabaseSeeder
{
    private static readonly Random Rng = new();
    public static void Seed(HandballDbContext db)
    {
        var logPath = Path.Combine(Path.GetDirectoryName(db.DbPath) ?? "", "seeding_log.txt");
        File.WriteAllText(logPath, $"--- Seeding Log {DateTime.Now} ---\n");

        void Log(string msg)
        {
            Console.WriteLine(msg);
            File.AppendAllText(logPath, msg + "\n");
        }

        if (db.Teams.Any())
        {
            Log($"Database already contains {db.Teams.Count()} teams. Skipping initial seed.");
            return;
        }

        var files = SeedDataSource.GetInitialTeamFiles();
        if (files.Count == 0)
        {
            Log("CRITICAL: No embedded Data/InitialData seed resources found in HandballManager.Core.");
            return;
        }

        Log($"Found {files.Count} embedded JSON files to process.");

        foreach (var (file, json) in files)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var teamData = JsonSerializer.Deserialize<Team>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

                if (teamData != null && !string.IsNullOrEmpty(teamData.Name))
                {
                    Log($"Seeding team: {teamData.Name} from {fileName}...");

                    var players = teamData.Players;
                    
                    // Detect Competition Name from folder
                    string competitionName = "Liga Florilor"; // Default
                    if (file.Contains("NBI", StringComparison.OrdinalIgnoreCase))
                    {
                        competitionName = "NB I";
                    }
                    else if (file.Contains("LFHDivision1", StringComparison.OrdinalIgnoreCase))
                    {
                        competitionName = "Ligue Butagaz Énergie";
                    }
                    else if (file.Contains("Kvindeligaen", StringComparison.OrdinalIgnoreCase))
                    {
                        competitionName = "Kvindeligaen";
                    }
                    teamData.CompetitionName = competitionName;

                    // Assign LogoPath based on fileName
                    string logoName = fileName.ToLower();
                    teamData.LogoPath = logoName switch
                    {
                        "baia_mare.json" => "baiamare.png",
                        "brasov.json" => "coronabrasov.png",
                        "craiova.json" => "craiova.png",
                        "csm_bucuresti.json" => "csmbucuresti.png",
                        "csm_galati.json" => "csmgalati.png",
                        "dunarea_braila.json" => "braila.png",
                        "gloria_bistrita.json" => "bistrita.png",
                        "ramnicu_valcea.json" => "valcea.png",
                        "rapid_bucuresti.json" => "rapidbucuresti.png",
                        "slatina.json" => "slatina.png",
                        "targu_jiu.json" => "targujiu.png",
                        "zalau.json" => "zalau.png",
                        // Hungarian Teams
                        "gyor.json" => "Hungary/gyor.png",
                        "dvsc.json" => "Hungary/dvsc.png",
                        "ferencvaros.json" => "Hungary/ferencvaros.png",
                        "esztergomi.json" => "Hungary/esztergomi.png",
                        "vaci_nkse.json" => "Hungary/vaci.png",
                        "alba_fehervar.json" => "Hungary/alba.png",
                        "kisvarda.json" => "Hungary/kisvarda.png",
                        "mosonmagyarovar.json" => "Hungary/mosonmagyarovar.png",
                        "szombathely.json" => "Hungary/szombathely.png",
                        "vasas_sc.json" => "Hungary/vasas.png",
                        "budaors.json" => "Hungary/budaors.png",
                        "neka.json" => "Hungary/neka.png",
                        "kozarmisleny_se.json" => "Hungary/kozarmisleny.png",
                        "dunaujvarosi_ka.json" => "Hungary/dunaujvaros.png",
                        // French Teams (Ligue Butagaz Énergie)
                        "achenheim_truchtersheim.json" => "France/achenheim.png",
                        "brest_bretagne.json" => "France/brestbretagne.png",
                        "chambray_touraine.json" => "France/chambray.png",
                        "esbf_besancon.json" => "France/besancon.png",
                        "havre.json" => "France/havre.png",
                        "jda_dijon.json" => "France/dijon.png",
                        "metz.json" => "France/metz.png",
                        "ogc_nice.json" => "France/ogcnice.png",
                        "paris92.json" => "France/paris92.png",
                        "plan_de_cuques.json" => "France/plandecuques.png",
                        "sahbph.json" => "France/sahbph.png",
                        "sambre_avesnois.json" => "France/sambreavesnois.png",
                        "stella_saint_maur.json" => "France/stellasaintmaur.png",
                        "toulon_metropole.json" => "France/toulon.png",
                        // Kvindeligaen (Denmark)
                        "aalborg.json" => "Denmark/ehaalborg.png",
                        "bjerringbro.json" => "Denmark/bjerringbro.png",
                        "esbjerg.json" => "Denmark/esbjerg.png",
                        "hh_elite.json" => "Denmark/hhelite.png",
                        "hoj.json" => "Denmark/hoj.png",
                        "ikast.json" => "Denmark/ikast.png",
                        "kobenhavn.json" => "Denmark/kobenhavn.png",
                        "nykobing.json" => "Denmark/nykobing.png",
                        "odense.json" => "Denmark/odense.png",
                        "ringkobing.json" => "Denmark/ringkobing.png",
                        "silkeborg.json" => "Denmark/silkeborg.png",
                        "skanderborg.json" => "Denmark/skanderborg.png",
                        "sonderjyske.json" => "Denmark/sonderjyske.png",
                        "viborg.json" => "Denmark/viborg.png",
                        _ => string.Empty
                    };

                    // For Romanian teams, they are in the root of teamlogo folder, but better to be explicit
                    if (!teamData.LogoPath.Contains("/") && !string.IsNullOrEmpty(teamData.LogoPath))
                    {
                        teamData.LogoPath = "Romania/" + teamData.LogoPath;
                    }

                    // Handle StadiumImage prefixing
                    if (!string.IsNullOrEmpty(teamData.StadiumImage) && !teamData.StadiumImage.Contains("/"))
                    {
                        string countryDir = competitionName switch
                        {
                            "NB I" => "Hungary",
                            "Ligue Butagaz Énergie" => "France",
                            "Kvindeligaen" => "Denmark",
                            _ => "Romania"
                        };
                        teamData.StadiumImage = countryDir + "/" + teamData.StadiumImage;
                    }

                    // Pre-calculate player wages so we can set the team's wage budget correctly
                    foreach (var player in players)
                    {
                        player.MonthlyWage = Services.TransferService.EstimateRequestedMonthlyWage(player);
                        
                        if (player.Height == 0) player.Height = Rng.Next(170, 195);
                        if (player.Weight == 0) player.Weight = Rng.Next(65, 95);

                        int currentYear = 2025;
                        int addedYears = player.Age < 25 ? Rng.Next(2, 6) : (player.Age <= 30 ? Rng.Next(2, 4) : Rng.Next(1, 3));
                        player.ContractEndDate = new DateTime(currentYear + addedYears, 6, 30);
                    }

                    // Calculate expected wages
                    decimal yearlyWage = players.Sum(p => (decimal)p.MonthlyWage * 12);
                    decimal weeklyWage = yearlyWage / 52m;

                    // Wage budget matches total wages exactly from the start
                    teamData.WageBudget = Math.Round(weeklyWage, 0);

                    // The total available budget for the club
                    teamData.ClubBalance = teamData.Budget;

                    // Transfer budget is the rest of the club balance after securing a year's worth of wages
                    teamData.TransferBudget = Math.Max(0, teamData.ClubBalance - (teamData.WageBudget * 52m));

                    if (teamData.TransferBudget == 0)
                    {
                        teamData.ClubBalance = teamData.WageBudget * 52m;
                    }

                    teamData.Players = new List<Player>();

                    db.Teams.Add(teamData);
                    db.SaveChanges();

                    foreach (var player in players)
                    {
                        player.TeamId = teamData.Id;
                        db.Players.Add(player);
                    }

                    db.LeagueEntries.Add(new LeagueEntry { TeamId = teamData.Id, CompetitionName = competitionName });
                    db.SaveChanges();
                    Log($"Successfully seeded {teamData.Name} with {players.Count} players.");
                }
                else
                {
                    Log($"Warning: {fileName} deserialized to null or empty team name.");
                }
            }
            catch (JsonException jex)
            {
                Log($"JSON Error in {fileName}: {jex.Message}");
                if (jex.InnerException != null) Log($"  Inner: {jex.InnerException.Message}");
            }
            catch (Exception ex)
            {
                Log($"Error seeding from {fileName}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        db.SaveChanges();
        Log($"Successfully seeded {db.Teams.Count()} teams and players.");

        // Assign Club Reputations, Stadium Capacities, and AI Managers
        var seededTeams = db.Teams.Include(t => t.Manager).ToList();
        var managerFirstNames = new[] { "Elena", "Maria", "Ioana", "Cristina", "Ana", "Mihaela", "Daniela", "Adriana", "Laura", "Simona", "Alina", "Carmen" };
        var managerLastNames = new[] { "Popescu", "Ionescu", "Popa", "Dumitrescu", "Stan", "Stoica", "Gheorghe", "Matei", "Ciobanu", "Rusu", "Moldovan", "Dinu" };
        var managerCities = new[] { "București", "Cluj-Napoca", "Timișoara", "Iași", "Constanța", "Craiova", "Brașov", "Galați", "Oradea", "Arad", "Sibiu", "Pitești" };

        foreach (var team in seededTeams)
        {
            // Assign Club Reputation and Stadium Capacity
            var nameLower = team.Name.ToLower();
            if (team.ClubReputation == Models.ReputationLevel.Local && (nameLower.Contains("csm bucureşti") || nameLower.Contains("csm bucurești")))
            {
                team.ClubReputation = Models.ReputationLevel.International;
            }
            else if (team.ClubReputation == Models.ReputationLevel.Local && (nameLower.Contains("rapid") || nameLower.Contains("brașov") || nameLower.Contains("brasov") || nameLower.Contains("vâlcea") || nameLower.Contains("valcea") || nameLower.Contains("bistrița") || nameLower.Contains("bistrita")))
            {
                team.ClubReputation = Models.ReputationLevel.National;
            }
            else if (team.ClubReputation == Models.ReputationLevel.Local && (nameLower.Contains("craiova") || nameLower.Contains("zalău") || nameLower.Contains("zalau") || nameLower.Contains("brăila") || nameLower.Contains("braila") || nameLower.Contains("baia mare")))
            {
                team.ClubReputation = Models.ReputationLevel.Regional;
            }

            // Stadium Capacity Fallbacks (if still at default value)
            if (team.StadiumCapacity == 2000)
            {
                if (team.ClubReputation == Models.ReputationLevel.International) team.StadiumCapacity = 5300;
                else if (team.ClubReputation == Models.ReputationLevel.National) team.StadiumCapacity = nameLower.Contains("rapid") ? 1500 : Rng.Next(2500, 4000);
                else if (team.ClubReputation == Models.ReputationLevel.Regional) team.StadiumCapacity = Rng.Next(1800, 3000);
                else team.StadiumCapacity = Rng.Next(1200, 2200);
            }

            // If we have a StadiumImage but no StadiumName from JSON (unlikely but safe), we could do something here.
            // But actually we have both. The above block just ensures we have fallback values.

            // ── Club Facilities ────────────────────────────────────────────────────
            // Hardcoded facility levels per club (training, youth) as provided
            var facilityMap = new Dictionary<string, (int Training, int Youth)>
            {
                // Romania – Liga Florilor
                ["baia_mare"]          = (2, 5),  // Average, High standard
                ["brasov"]             = (3, 2),  // Adequate, Average
                ["craiova"]            = (2, 1),  // Average, Below average
                ["csm_bucuresti"]      = (6, 4),  // Fantastic, Modern
                ["csm_galati"]         = (0, 1),  // Low standard, Below average
                ["dunarea_braila"]     = (5, 3),  // High standard, Adequate
                ["gloria_bistrita"]    = (5, 4),  // High standard, Modern
                ["ramnicu_valcea"]     = (4, 5),  // Modern, High standard
                ["rapid_bucuresti"]    = (5, 3),  // High standard, Adequate
                ["slatina"]            = (1, 2),  // Below average, Average
                ["targu_jiu"]          = (1, 0),  // Below average, Low standard
                ["zalau"]              = (2, 5),  // Average, High standard

                // Denmark – Kvindeligaen
                ["aalborg"]            = (1, 2),  // Below average, Average
                ["bjerringbro"]        = (1, 3),  // Below average, Adequate
                ["esbjerg"]            = (6, 5),  // Fantastic, High standard
                ["hh_elite"]           = (2, 2),  // Average, Average
                ["hoj"]                = (0, 1),  // Low standard, Below average
                ["ikast"]              = (5, 6),  // High standard, Fantastic
                ["kobenhavn"]          = (4, 3),  // Modern, Adequate
                ["nykobing"]           = (4, 3),  // Modern, Adequate
                ["odense"]             = (6, 4),  // Fantastic, Modern
                ["ringkobing"]         = (1, 2),  // Below average, Average
                ["silkeborg"]          = (2, 4),  // Average, Modern
                ["skanderborg"]        = (2, 5),  // Average, High standard
                ["sonderjyske"]        = (3, 2),  // Adequate, Average
                ["viborg"]             = (4, 5),  // Modern, High standard

                // France – Ligue Butagaz Énergie
                ["achenheim_truchtersheim"] = (0, 1),  // Low standard, Below average
                ["brest_bretagne"]     = (6, 5),  // Fantastic, High standard
                ["chambray_touraine"]  = (3, 2),  // Adequate, Average
                ["esbf_besancon"]      = (4, 5),  // Modern, High standard
                ["havre"]              = (1, 2),  // Below average, Average
                ["jda_dijon"]          = (3, 3),  // Adequate, Adequate
                ["metz"]               = (6, 6),  // Fantastic, Fantastic
                ["ogc_nice"]           = (2, 2),  // Average, Average
                ["paris92"]            = (4, 5),  // Modern, High standard
                ["plan_de_cuques"]     = (2, 1),  // Average, Below average
                ["sahbph"]             = (1, 2),  // Below average, Average
                ["sambre_avesnois"]    = (0, 0),  // Low standard, Low standard
                ["stella_saint_maur"]  = (1, 1),  // Below average, Below average
                ["toulon_metropole"]   = (2, 3),  // Average, Adequate

                // Hungary – NB I
                ["alba_fehervar"]      = (2, 3),  // Average, Adequate
                ["budaors"]            = (1, 2),  // Below average, Average
                ["dunaujvarosi_ka"]    = (2, 5),  // Average, High standard
                ["dvsc"]               = (5, 5),  // High standard, High standard
                ["esztergomi"]         = (3, 1),  // Adequate, Below average
                ["ferencvaros"]        = (6, 5),  // Fantastic, High standard
                ["gyor"]               = (6, 6),  // Fantastic, Fantastic
                ["kisvarda"]           = (1, 2),  // Below average, Average
                ["kozarmisleny_se"]    = (0, 1),  // Low standard, Below average
                ["mosonmagyarovar"]    = (4, 3),  // Modern, Adequate
                ["neka"]               = (4, 6),  // Modern, Fantastic
                ["szombathely"]        = (1, 2),  // Below average, Average
                ["vaci_nkse"]          = (3, 5),  // Adequate, High standard
                ["vasas_sc"]           = (2, 3),  // Average, Adequate
            };

            // Try to match from the file that seeded this team
            var matchedFile = files.FirstOrDefault(f =>
            {
                var teamJson = System.Text.Json.JsonSerializer.Deserialize<Team>(
                    f.Json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return teamJson?.Name == team.Name;
            });

            string facilityKey = matchedFile.Path != null
                ? Path.GetFileNameWithoutExtension(matchedFile.Path).ToLower()
                : "";

            if (facilityMap.TryGetValue(facilityKey, out var levels))
            {
                team.TrainingFacilityLevel = levels.Training;
                team.YouthFacilityLevel = levels.Youth;
            }
            else
            {
                // Default: Average for both
                team.TrainingFacilityLevel = 2;
                team.YouthFacilityLevel = 2;
            }

            // Create AI Manager if one doesn't already exist
            if (team.Manager != null)
            {
                var mgr = team.Manager;
                mgr.TeamId = team.Id;
                mgr.IsPlayerManager = false;
                
                // Set default history if none provided
                if (string.IsNullOrEmpty(mgr.ClubHistoryJson) || mgr.ClubHistoryJson == "[]")
                {
                    mgr.ClubHistoryJson = System.Text.Json.JsonSerializer.Serialize(new[]
                    {
                        new Models.ManagerClubHistory
                        {
                            ClubName = team.Name,
                            StartDate = "2023",
                            EndDate = ""
                        }
                    });
                }
            }
            else
            {
                var mgr = new Models.Manager
                {
                    FirstName = managerFirstNames[Rng.Next(managerFirstNames.Length)],
                    LastName = managerLastNames[Rng.Next(managerLastNames.Length)],
                    Birthdate = new DateTime(Rng.Next(1965, 1990), Rng.Next(1, 13), Rng.Next(1, 28)),
                    PlaceOfBirth = managerCities[Rng.Next(managerCities.Length)],
                    Nationality = "ROU",
                    License = Models.CoachLicense.Level3,
                    Reputation = team.ClubReputation switch
                    {
                        Models.ReputationLevel.International => Models.ReputationLevel.National,
                        Models.ReputationLevel.National => Models.ReputationLevel.National,
                        Models.ReputationLevel.Regional => (Models.ReputationLevel)Rng.Next(1, 3), // Regional or National
                        _ => (Models.ReputationLevel)Rng.Next(0, 2) // Local or Regional
                    },
                    Motivation = Rng.Next(8, 17),
                    YouthDevelopment = Rng.Next(8, 17),
                    Discipline = Rng.Next(8, 17),
                    Adaptability = Rng.Next(8, 17),
                    TimeoutTalks = Rng.Next(8, 17),
                    TeamId = team.Id,
                    IsPlayerManager = false,
                    ClubHistoryJson = System.Text.Json.JsonSerializer.Serialize(new[]
                    {
                        new Models.ManagerClubHistory
                        {
                            ClubName = team.Name,
                            StartDate = "2023",
                            EndDate = ""
                        }
                    })
                };
                db.Managers.Add(mgr);
            }
        }

        try
        {
            db.SaveChanges();
            Log("Successfully seeded AI managers and club reputations.");
        }
        catch (DbUpdateException due)
        {
            Log($"--- CRITICAL DB SAVE ERROR ---");
            Log($"Message: {due.Message}");
            if (due.InnerException != null)
            {
                Log($"Inner Exception: {due.InnerException.Message}");
            }
            
            // Try to find which entities are causing the trouble
            foreach (var entry in due.Entries)
            {
                Log($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
                if (entry.Entity is Models.Manager mgr)
                {
                    Log($"Manager: {mgr.FirstName} {mgr.LastName}, TeamId: {mgr.TeamId}");
                }
            }
            throw;
        }

        var allTeams = db.Teams.ToList();
        int GetTeamId(string historicalName)
        {
            // Direct match
            var t = allTeams.FirstOrDefault(x => x.Name.Equals(historicalName, StringComparison.OrdinalIgnoreCase));
            if (t != null) return t.Id;

            // Mapping for historical names
            return historicalName switch
            {
                "Rulmentul Brașov" => allTeams.FirstOrDefault(x => x.Name.Contains("Brașov"))?.Id ?? 0,
                "HCM Baia Mare" => allTeams.FirstOrDefault(x => x.Name.Contains("Baia Mare"))?.Id ?? 0,
                "Chimistul Râmnicu Vâlcea" or "Oltchim Râmnicu Vâlcea" => allTeams.FirstOrDefault(x => x.Name.Contains("Vâlcea"))?.Id ?? 0,
                "Silcotub Zalău" => allTeams.FirstOrDefault(x => x.Name.Contains("Zalău"))?.Id ?? 0,
                "FC Midtjylland Håndbold" or "Herning-Ikast Håndbold" => allTeams.FirstOrDefault(x => x.CompetitionName == "Kvindeligaen" && x.Name.Contains("Ikast"))?.Id ?? 0,
                "Viborg HK" => allTeams.FirstOrDefault(x => x.Name.Contains("Viborg"))?.Id ?? 0,
                "Team Esbjerg" => allTeams.FirstOrDefault(x => x.Name.Contains("Esbjerg"))?.Id ?? 0,
                "Odense Håndbold" => allTeams.FirstOrDefault(x => x.Name.Contains("Odense"))?.Id ?? 0,
                "København Håndbold" => allTeams.FirstOrDefault(x => x.Name.Contains("København") || x.Name.Contains("Kobenhavn"))?.Id ?? 0,
                "Nykøbing Falster Håndboldklub" => allTeams.FirstOrDefault(x => x.Name.Contains("Nykøbing") || x.Name.Contains("Nykobing"))?.Id ?? 0,
                "Bjerringbro FH" => allTeams.FirstOrDefault(x => x.Name.Contains("Bjerringbro"))?.Id ?? 0,
                _ => 0
            };
        }

        // Seed Champions
        var champsJson = SeedDataSource.ReadText("Data/Past Champions/Liga Florilor/champions.json");
        if (!db.ChampionRecords.Any() && champsJson != null)
        {
            try
            {
                var champsList = JsonSerializer.Deserialize<List<ChampionRecord>>(champsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (champsList != null)
                {
                    foreach (var r in champsList) 
                    {
                        r.TeamId = GetTeamId(r.TeamName);
                        r.CompetitionName = "Liga Florilor";
                    }
                    db.ChampionRecords.AddRange(champsList);
                    Log($"Successfully seeded {champsList.Count} historical champions.");
                }
            }
            catch (Exception ex) { Log($"Error seeding champions: {ex.Message}"); }
        }

        // Seed NBI Champions
        var nbiChampsJson = SeedDataSource.ReadText("Data/Past Champions/NBI/champions.json");
        if (!db.ChampionRecords.Any(r => r.CompetitionName == "NB I") && nbiChampsJson != null)
        {
            try
            {
                var champsList = JsonSerializer.Deserialize<List<ChampionRecord>>(nbiChampsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (champsList != null)
                {
                    foreach (var r in champsList) 
                    {
                        r.TeamId = GetTeamId(r.TeamName);
                        r.CompetitionName = "NB I";
                    }
                    db.ChampionRecords.AddRange(champsList);
                    Log($"Successfully seeded {champsList.Count} historical NBI champions.");
                }
            }
            catch (Exception ex) { Log($"Error seeding NBI champions: {ex.Message}"); }
        }

        // Seed French Champions (Ligue Butagaz Énergie)
        var frChampsJson = SeedDataSource.ReadText("Data/Past Champions/Ligue Butagaz Énergie/champions.json");
        if (!db.ChampionRecords.Any(r => r.CompetitionName == "Ligue Butagaz Énergie") && frChampsJson != null)
        {
            try
            {
                var champsList = JsonSerializer.Deserialize<List<ChampionRecord>>(frChampsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (champsList != null)
                {
                    foreach (var r in champsList)
                    {
                        r.TeamId = GetTeamId(r.TeamName);
                        r.CompetitionName = "Ligue Butagaz Énergie";
                    }
                    db.ChampionRecords.AddRange(champsList);
                    Log($"Successfully seeded {champsList.Count} historical French champions.");
                }
            }
            catch (Exception ex) { Log($"Error seeding French champions: {ex.Message}"); }
        }

        // Kvindeligaen champions
        var dkChampsJson = SeedDataSource.ReadText("Data/Past Champions/Kvindeligaen/champions.json");
        if (!db.ChampionRecords.Any(r => r.CompetitionName == "Kvindeligaen") && dkChampsJson != null)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ChampionRecord>>(dkChampsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (list != null)
                {
                    foreach (var r in list)
                    {
                        r.TeamId = GetTeamId(r.TeamName);
                        r.CompetitionName = "Kvindeligaen";
                    }
                    db.ChampionRecords.AddRange(list);
                    Log($"Successfully seeded {list.Count} Kvindeligaen champions.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Kvindeligaen champions: {ex.Message}"); }
        }

        // Seed Cup Winners
        var roCupJson = SeedDataSource.ReadText("Data/Past Champions/Cupa Romaniei/cup_winners.json");
        if (!db.CupWinnerRecords.Any() && roCupJson != null)
        {
            try
            {
                var cupList = JsonSerializer.Deserialize<List<CupWinnerRecord>>(roCupJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cupList != null)
                {
                    foreach (var r in cupList) r.TeamId = GetTeamId(r.TeamName);
                    db.CupWinnerRecords.AddRange(cupList);
                    Log($"Successfully seeded {cupList.Count} historical cup winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding cup winners: {ex.Message}"); }
        }

        // Seed Magyar Kupa Winners
        var huCupJson = SeedDataSource.ReadText("Data/Past Champions/Magyar Kupa/cup_winners.json");
        if (!db.CupWinnerRecords.Any(r => r.CompetitionName == "NB I") && huCupJson != null)
        {
            try
            {
                var cupList = JsonSerializer.Deserialize<List<CupWinnerRecord>>(huCupJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cupList != null)
                {
                    foreach (var r in cupList)
                    {
                        r.CompetitionName = "NB I";
                        // Map historical names to current team IDs
                        r.TeamId = r.TeamName switch
                        {
                            "Vasas" => allTeams.FirstOrDefault(x => x.Name.Contains("Vasas"))?.Id ?? 0,
                            "Ferencváros" => allTeams.FirstOrDefault(x => x.Name.Contains("Ferencváros"))?.Id ?? 0,
                            "Győri ETO" => allTeams.FirstOrDefault(x => x.Name.Contains("Győr"))?.Id ?? 0,
                            "Debreceni VSC" => allTeams.FirstOrDefault(x => x.Name.Contains("DVSC") || x.Name.Contains("Debrecen"))?.Id ?? 0,
                            "Dunaferr" => allTeams.FirstOrDefault(x => x.Name.Contains("Dunaújváros"))?.Id ?? 0,
                            _ => 0
                        };
                    }
                    db.CupWinnerRecords.AddRange(cupList);
                    Log($"Successfully seeded {cupList.Count} historical Magyar Kupa winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Magyar Kupa winners: {ex.Message}"); }
        }

        // Seed Coupe de France winners (stored under Ligue Butagaz Énergie competition key)
        var cdfJson = SeedDataSource.ReadText("Data/Past Champions/Coupe de France/cup_winners.json");
        if (!db.CupWinnerRecords.Any(r => r.CompetitionName == "Ligue Butagaz Énergie") && cdfJson != null)
        {
            try
            {
                var cupList = JsonSerializer.Deserialize<List<CupWinnerRecord>>(cdfJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cupList != null)
                {
                    foreach (var r in cupList)
                    {
                        r.CompetitionName = "Ligue Butagaz Énergie";
                        r.TeamId = GetTeamId(r.TeamName);
                    }
                    db.CupWinnerRecords.AddRange(cupList);
                    Log($"Successfully seeded {cupList.Count} historical Coupe de France winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Coupe de France winners: {ex.Message}"); }
        }

        // Landspokalturnering (Danish cup, keyed by Kvindeligaen)
        var dkCupJson = SeedDataSource.ReadText("Data/Past Champions/Landspokalturnering/cup_winners.json");
        if (!db.CupWinnerRecords.Any(r => r.CompetitionName == "Kvindeligaen") && dkCupJson != null)
        {
            try
            {
                var cupList = JsonSerializer.Deserialize<List<CupWinnerRecord>>(dkCupJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cupList != null)
                {
                    foreach (var r in cupList)
                    {
                        r.CompetitionName = "Kvindeligaen";
                        r.TeamId = GetTeamId(r.TeamName);
                    }
                    db.CupWinnerRecords.AddRange(cupList);
                    Log($"Successfully seeded {cupList.Count} Landspokalturnering cup winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Danish cup winners: {ex.Message}"); }
        }

        // Supercupa României (Liga Florilor — historical finals calendar year stored as Season string)
        var roScJson = SeedDataSource.ReadText("Data/Past Champions/Supercupa Romaniei/supercup_winners.json");
        if (!db.SupercupWinnerRecords.Any(r => r.CompetitionName == "Liga Florilor") && roScJson != null)
        {
            try
            {
                var roScList = JsonSerializer.Deserialize<List<SupercupWinnerRecord>>(roScJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (roScList != null)
                {
                    foreach (var r in roScList)
                    {
                        r.CompetitionName = "Liga Florilor";
                        r.TeamId = GetTeamId(r.TeamName);
                    }

                    db.SupercupWinnerRecords.AddRange(roScList);
                    Log($"Successfully seeded {roScList.Count} Supercupa României winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Romanian supercup winners: {ex.Message}"); }
        }

        // Bambuni Supercup (women's — Kvindeligaen key)
        var dkScJson = SeedDataSource.ReadText("Data/Past Champions/Bambuni Supercup/supercup_winners.json");
        if (!db.SupercupWinnerRecords.Any(r => r.CompetitionName == "Kvindeligaen") && dkScJson != null)
        {
            try
            {
                var scList = JsonSerializer.Deserialize<List<SupercupWinnerRecord>>(dkScJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (scList != null)
                {
                    foreach (var r in scList)
                    {
                        r.CompetitionName = "Kvindeligaen";
                        r.TeamId = GetTeamId(r.TeamName);
                    }
                    db.SupercupWinnerRecords.AddRange(scList);
                    Log($"Successfully seeded {scList.Count} Bambuni Supercup winners.");
                }
            }
            catch (Exception ex) { Log($"Error seeding Danish supercup winners: {ex.Message}"); }
        }

        db.SaveChanges();
        Log("--- Seeding Complete ---");
    }
}
