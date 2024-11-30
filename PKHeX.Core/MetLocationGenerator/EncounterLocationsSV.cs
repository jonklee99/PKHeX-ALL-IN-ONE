using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace PKHeX.Core.MetLocationGenerator
{
    public static class EncounterLocationsSV
    {
        public static void GenerateEncounterDataJSON(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting JSON generation process for encounters in Scarlet/Violet.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var pt = PersonalTable.SV;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for SV loaded.");

                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots
                ProcessRegularEncounters(encounterData, gameStrings, pt, errorLogger);

                // Process 7-Star Raid encounters
                ProcessSevenStarRaids(encounterData, gameStrings, pt, errorLogger);

                // Process static encounters for both versions
                ProcessStaticEncounters(Encounters9.Encounter_SV, "Both", encounterData, gameStrings, pt, errorLogger);
                ProcessStaticEncounters(Encounters9.StaticSL, "Scarlet", encounterData, gameStrings, pt, errorLogger);
                ProcessStaticEncounters(Encounters9.StaticVL, "Violet", encounterData, gameStrings, pt, errorLogger);

                // Process fixed encounters
                ProcessFixedEncounters(encounterData, gameStrings, pt, errorLogger);

                // Process Tera Raid encounters
                ProcessTeraRaidEncounters(encounterData, gameStrings, pt, errorLogger);

                // Process distribution encounters
                ProcessDistributionEncounters(encounterData, gameStrings, pt, errorLogger);

                // Process outbreak encounters
                ProcessOutbreakEncounters(encounterData, gameStrings, pt, errorLogger);

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(encounterData, jsonOptions);

                File.WriteAllText(outputPath, jsonString, new UTF8Encoding(false));

                errorLogger.WriteLine($"[{DateTime.Now}] JSON file generated successfully without BOM at: {outputPath}");
            }
            catch (Exception ex)
            {
                using var errorLogger = new StreamWriter(errorLogPath, true, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] An error occurred: {ex.Message}");
                errorLogger.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private static void ProcessRegularEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var area in Encounters9.Slots)
            {
                var locationId = area.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {locationId}";

                foreach (var slot in area.Slots)
                {
                    AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, slot.Species, slot.Form, locationName, locationId,
                        slot.LevelMin, slot.LevelMax, "Wild", false, false, null, "Both", SizeType9.RANDOM, 0);
                }
            }
        }

        private static void ProcessSevenStarRaids(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in Encounters9.Might)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)EncounterMight9.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = "A Crystal Cavern";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName,
                    EncounterMight9.Location, encounter.Level, encounter.Level, "7-Star Raid", encounter.Shiny == Shiny.Never,
                    false, null, "Both", encounter.ScaleType, encounter.Scale);
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic9[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                var locationId = encounter.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {locationId}";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName, locationId,
                    encounter.Level, encounter.Level, "Static", encounter.Shiny == Shiny.Never, false,
                    encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null, versionName, SizeType9.RANDOM, 0);
            }
        }

        private static void ProcessFixedEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in Encounters9.Fixed)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)encounter.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName,
                    encounter.Location, encounter.Level, encounter.Level, "Fixed", false, false, null, "Both", SizeType9.RANDOM, 0);
            }
        }

        private static void ProcessTeraRaidEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            ProcessTeraRaidEncountersForGroup(Encounters9.TeraBase, encounterData, gameStrings, pt, errorLogger, "Paldea");
            ProcessTeraRaidEncountersForGroup(Encounters9.TeraDLC1, encounterData, gameStrings, pt, errorLogger, "Kitakami");
            ProcessTeraRaidEncountersForGroup(Encounters9.TeraDLC2, encounterData, gameStrings, pt, errorLogger, "Blueberry");
        }

        private static void ProcessTeraRaidEncountersForGroup(EncounterTera9[] encounters, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger, string groupName)
        {
            foreach (var encounter in encounters)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)EncounterTera9.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = "Tera Raid Den";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName,
                    EncounterTera9.Location, encounter.Level, encounter.Level, $"{encounter.Stars}★ Tera Raid {groupName}",
                    encounter.Shiny == Shiny.Never, false, null,
                    encounter.IsAvailableHostScarlet && encounter.IsAvailableHostViolet ? "Both" :
                    (encounter.IsAvailableHostScarlet ? "Scarlet" : "Violet"), SizeType9.RANDOM, 0);
            }
        }

        private static void ProcessDistributionEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in Encounters9.Dist)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)EncounterDist9.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = "Distribution Raid Den";

                var versionAvailability = GetVersionAvailability(encounter);

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName,
                    EncounterDist9.Location, encounter.Level, encounter.Level, $"Distribution Raid {encounter.Stars}★",
                    encounter.Shiny == Shiny.Never, false, null, versionAvailability, encounter.ScaleType, encounter.Scale);
            }
        }

        private static string GetVersionAvailability(EncounterDist9 encounter)
        {
            bool availableInScarlet = encounter.RandRate0TotalScarlet > 0 || encounter.RandRate1TotalScarlet > 0 ||
                                    encounter.RandRate2TotalScarlet > 0 || encounter.RandRate3TotalScarlet > 0;

            bool availableInViolet = encounter.RandRate0TotalViolet > 0 || encounter.RandRate1TotalViolet > 0 ||
                                   encounter.RandRate2TotalViolet > 0 || encounter.RandRate3TotalViolet > 0;

            if (availableInScarlet && availableInViolet)
                return "Both";
            if (availableInScarlet)
                return "Scarlet";
            if (availableInViolet)
                return "Violet";

            return "Unknown";
        }

        private static void ProcessOutbreakEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in Encounters9.Outbreak)
            {
                var locationName = gameStrings.GetLocationName(false, encounter.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName,
                    encounter.Location, encounter.LevelMin, encounter.LevelMax, "Outbreak", encounter.Shiny == Shiny.Never,
                    false, null, "Both", encounter.IsForcedScaleRange ? SizeType9.VALUE : SizeType9.RANDOM,
                    encounter.IsForcedScaleRange ? encounter.ScaleMin : (byte)0);
            }
        }

        private static void AddEncounterInfoWithEvolutions(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings,
            PersonalTable9SV pt, StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, int locationId,
            int minLevel, int maxLevel, string encounterType, bool isShinyLocked, bool isGift, string fixedBall,
            string encounterVersion, SizeType9 sizeType, byte sizeValue)
        {
            var personalInfo = pt.GetFormEntry(speciesIndex, form);
            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} form {form} not present in SV. Skipping.");
                return;
            }

            // Process base species
            AddSingleEncounterInfo(encounterData, gameStrings, errorLogger, speciesIndex, form, locationName, locationId,
                minLevel, maxLevel, encounterType, isShinyLocked, isGift, fixedBall, encounterVersion, sizeType, sizeValue);

            // Track processed species/forms to avoid duplicates
            var processedForms = new HashSet<(ushort Species, byte Form)>();
            processedForms.Add((speciesIndex, form));

            // Process all evolutions recursively
            ProcessEvolutionLine(encounterData, gameStrings, pt, errorLogger, speciesIndex, form, locationName, locationId,
                minLevel, encounterType, isShinyLocked, isGift, fixedBall, encounterVersion, sizeType, sizeValue, processedForms);
        }

        private static int GetMinEvolutionLevel(ushort baseSpecies, ushort evolvedSpecies)
        {
            var tree = EvolutionTree.Evolves9;
            var pk = new PK9 { Species = baseSpecies, Form = 0, CurrentLevel = 100, Version = GameVersion.VL };
            int maxLevel = 1;

            var evos = tree.Forward.GetForward(baseSpecies, 0);
            foreach (var evo in evos.Span)
            {
                if (evo.Species == evolvedSpecies)
                {
                    maxLevel = Math.Max(maxLevel, Math.Max(evo.Level, evo.LevelUp));
                }
                else
                {
                    var nextEvos = tree.Forward.GetForward(evo.Species, 0);
                    foreach (var nextEvo in nextEvos.Span)
                    {
                        if (nextEvo.Species == evolvedSpecies)
                        {
                            int evoLevel = Math.Max(evo.Level, evo.LevelUp);
                            int nextEvoLevel = Math.Max(nextEvo.Level, nextEvo.LevelUp);
                            maxLevel = Math.Max(maxLevel, Math.Max(evoLevel, nextEvoLevel));
                        }
                    }
                }
            }

            return maxLevel;
        }

        private static void ProcessEvolutionLine(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings,
            PersonalTable9SV pt, StreamWriter errorLogger, ushort species, byte form, string locationName, int locationId,
            int baseLevel, string encounterType, bool isShinyLocked, bool isGift, string fixedBall, string encounterVersion,
            SizeType9 sizeType, byte sizeValue, HashSet<(ushort Species, byte Form)> processedForms)
        {
            var personalInfo = pt.GetFormEntry(species, form);
            if (personalInfo == null || !personalInfo.IsPresentInGame)
                return;

            var nextEvolutions = TraverseEvolutions(species, form, pt, processedForms);
            foreach (var (evoSpecies, evoForm) in nextEvolutions)
            {
                // Skip if we've already processed this form
                if (!processedForms.Add((evoSpecies, evoForm)))
                    continue;

                var evoPersonalInfo = pt.GetFormEntry(evoSpecies, evoForm);
                if (evoPersonalInfo == null || !evoPersonalInfo.IsPresentInGame)
                    continue;

                // Get minimum evolution level
                var evolutionMinLevel = GetMinEvolutionLevel(species, evoSpecies);
                // Use the higher of the evolution requirement and base encounter level
                var minLevel = Math.Max(baseLevel, evolutionMinLevel);

                AddSingleEncounterInfo(encounterData, gameStrings, errorLogger, evoSpecies, evoForm, locationName, locationId,
                    minLevel, minLevel, encounterType, isShinyLocked, isGift, fixedBall, encounterVersion, sizeType, sizeValue);

                // Recursively process next evolutions
                ProcessEvolutionLine(encounterData, gameStrings, pt, errorLogger, evoSpecies, evoForm, locationName, locationId,
                    minLevel, encounterType, isShinyLocked, isGift, fixedBall, encounterVersion, sizeType, sizeValue, processedForms);
            }
        }

        private static List<(ushort Species, byte Form)> TraverseEvolutions(ushort species, byte form, PersonalTable9SV pt, HashSet<(ushort Species, byte Form)> processedForms)
        {
            var results = new List<(ushort Species, byte Form)>();
            var personalInfo = pt.GetFormEntry(species, form);

            if (personalInfo == null)
                return results;
            // Get evolutions (each species can have different forms)
            for (ushort evoSpecies = 1; evoSpecies < pt.MaxSpeciesID; evoSpecies++)
            {
                if (processedForms.Contains((evoSpecies, 0)))
                    continue;

                var evoPersonalInfo = pt.GetFormEntry(evoSpecies, 0);
                if (evoPersonalInfo == null || !evoPersonalInfo.IsPresentInGame)
                    continue;

                // Check if this species evolves from our current species
                if (evoPersonalInfo.HatchSpecies == species)
                {
                    // Get all valid forms for this evolved species
                    byte formCount = evoPersonalInfo.FormCount;
                    for (byte evoForm = 0; evoForm < formCount; evoForm++)
                    {
                        var formInfo = pt.GetFormEntry(evoSpecies, evoForm);
                        if (formInfo != null && formInfo.IsPresentInGame)
                        {
                            results.Add((evoSpecies, evoForm));
                        }
                    }
                }
            }

            return results;
        }

        private static string CombineVersions(string version1, string version2)
        {
            if (version1 == "Both" || version2 == "Both")
                return "Both";
            if ((version1 == "Scarlet" && version2 == "Violet") ||
                (version1 == "Violet" && version2 == "Scarlet"))
                return "Both";
            return version1; // Return existing version if they're the same
        }

        private static void AddSingleEncounterInfo(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings,
            StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, int locationId, int minLevel, int maxLevel,
            string encounterType, bool isShinyLocked, bool isGift, string fixedBall, string encounterVersion,
            SizeType9 sizeType, byte sizeValue)
        {
            string dexNumber = speciesIndex.ToString();
            if (form > 0)
                dexNumber += $"-{form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            var personalInfo = PersonalTable.SV.GetFormEntry(speciesIndex, form);
            string genderRatio = DetermineGenderRatio(personalInfo);

            var existingEncounter = encounterData[dexNumber].FirstOrDefault(e =>
                e.LocationId == locationId &&
                e.SpeciesIndex == speciesIndex &&
                e.Form == form &&
                e.EncounterType == encounterType &&
                e.Gender == genderRatio); 

            if (existingEncounter != null)
            {
                // If this is the same species in the same location, combine versions and keep lowest level
                existingEncounter.MinLevel = Math.Min(existingEncounter.MinLevel, minLevel);
                existingEncounter.MaxLevel = Math.Max(existingEncounter.MaxLevel, maxLevel);
                existingEncounter.EncounterVersion = CombineVersions(existingEncounter.EncounterVersion, encounterVersion);

                errorLogger.WriteLine($"[{DateTime.Now}] Updated existing encounter: {gameStrings.specieslist[speciesIndex]} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {existingEncounter.MinLevel}-{existingEncounter.MaxLevel}, " +
                    $"Type: {encounterType}, Version: {existingEncounter.EncounterVersion}, Gender: {genderRatio}");
            }
            else
            {
                encounterData[dexNumber].Add(new EncounterInfo
                {
                    SpeciesName = gameStrings.specieslist[speciesIndex],
                    SpeciesIndex = speciesIndex,
                    Form = form,
                    LocationName = locationName,
                    LocationId = locationId,
                    MinLevel = minLevel,
                    MaxLevel = maxLevel,
                    EncounterType = encounterType,
                    IsShinyLocked = isShinyLocked,
                    IsGift = isGift,
                    FixedBall = fixedBall,
                    EncounterVersion = encounterVersion,
                    SizeType = sizeType,
                    SizeValue = sizeValue,
                    Gender = genderRatio
                });

                errorLogger.WriteLine($"[{DateTime.Now}] Processed new encounter: {gameStrings.specieslist[speciesIndex]} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {minLevel}-{maxLevel}, " +
                    $"Type: {encounterType}, Version: {encounterVersion}, Gender: {genderRatio}");
            }
        }

        private static string DetermineGenderRatio(IPersonalInfo personalInfo)
        {
            if (personalInfo == null)
                return "Unknown";

            if (personalInfo.Genderless)
                return "Genderless";
            if (personalInfo.OnlyFemale)
                return "Female";
            if (personalInfo.OnlyMale)
                return "Male";

            // Handle regular gender ratios
            return personalInfo.Gender switch
            {
                0 => "Male",         // 100% Male
                254 => "Female",     // 100% Female
                255 => "Genderless", // Genderless
                _ => "Male, Female"  // Mixed gender ratio 
            };
        }

        private class EncounterInfo
        {
            public string SpeciesName { get; set; }
            public int SpeciesIndex { get; set; }
            public int Form { get; set; }
            public string LocationName { get; set; }
            public int LocationId { get; set; }
            public int MinLevel { get; set; }
            public int MaxLevel { get; set; }
            public string EncounterType { get; set; }
            public bool IsShinyLocked { get; set; }
            public bool IsGift { get; set; }
            public string FixedBall { get; set; }
            public string EncounterVersion { get; set; }
            public SizeType9 SizeType { get; set; }
            public byte SizeValue { get; set; }
            public string Gender { get; set; }
        }
    }
}
