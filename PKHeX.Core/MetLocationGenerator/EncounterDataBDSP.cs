using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace PKHeX.Core.MetLocationGenerator
{
    public static class EncounterDataBDSP
    {
        public static void GenerateEncounterDataJSON(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting JSON generation process for encounters in BDSP.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var pt = PersonalTable.BDSP;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for BDSP loaded.");

                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots
                ProcessWildEncounters(encounterData, gameStrings, errorLogger);

                // Process static encounters
                ProcessStaticEncounters(encounterData, gameStrings, errorLogger);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string jsonString = JsonSerializer.Serialize(encounterData, jsonOptions);

                using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (var streamWriter = new StreamWriter(fileStream, new UTF8Encoding(false)))
                {
                    streamWriter.Write(jsonString);
                }

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

        private static void ProcessWildEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            ProcessEncounterAreas(Encounters8b.SlotsBD, GameVersion.BD, encounterData, gameStrings, errorLogger);
            ProcessEncounterAreas(Encounters8b.SlotsSP, GameVersion.SP, encounterData, gameStrings, errorLogger);
        }

        private static void ProcessEncounterAreas(EncounterArea8b[] areas, GameVersion version, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var area in areas)
            {
                ProcessEncounterArea(area, version, encounterData, gameStrings, errorLogger);
            }
        }

        private static void ProcessEncounterArea(EncounterArea8b area, GameVersion version, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var locationName = gameStrings.GetLocationName(false, area.Location, 8, 8, GameVersion.BDSP);
            if (string.IsNullOrEmpty(locationName))
                locationName = $"Unknown Location {area.Location}";

            foreach (var slot in area.Slots)
            {
                ProcessEncounterSlot(slot, area, locationName, version, encounterData, gameStrings, errorLogger);
            }
        }

        private static void ProcessEncounterSlot(EncounterSlot8b slot, EncounterArea8b area, string locationName, GameVersion version, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var speciesName = gameStrings.specieslist[slot.Species];
            if (string.IsNullOrEmpty(speciesName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {slot.Species}. Skipping.");
                return;
            }

            AddEncounterInfoWithEvolutions(encounterData, gameStrings, errorLogger, slot.Species, slot.Form, locationName, area.Location,
                slot.LevelMin, slot.LevelMax, area.Type.ToString(), false, false, null, version.ToString(), slot.IsUnderground);
        }

        private static void ProcessStaticEncounterArray(EncounterStatic8b[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                var speciesName = gameStrings.specieslist[encounter.Species];
                if (string.IsNullOrEmpty(speciesName))
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {encounter.Species}. Skipping.");
                    continue;
                }

                var locationName = gameStrings.GetLocationName(false, encounter.Location, 8, 8, GameVersion.BDSP);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                AddEncounterInfoWithEvolutions(encounterData, gameStrings, errorLogger, encounter.Species, encounter.Form, locationName,
                    encounter.Location, encounter.Level, encounter.Level, "Static", encounter.Shiny == Shiny.Never, encounter.FixedBall != Ball.None,
                    encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null, versionName, false);
            }
        }

        private static int GetMinEvolutionLevel(ushort baseSpecies, ushort evolvedSpecies)
        {
            var tree = EvolutionTree.Evolves8b;
            var pk = new PB8 { Species = baseSpecies, Form = 0, CurrentLevel = 100, Version = GameVersion.BD };
            int maxLevel = 1;

            // Get possible evolutions forward from base species
            var evos = tree.Forward.GetForward(baseSpecies, 0);
            foreach (var evo in evos.Span)
            {
                if (evo.Species == evolvedSpecies)
                {
                    // Direct evolution - use level requirement
                    maxLevel = Math.Max(maxLevel, Math.Max(evo.Level, evo.LevelUp));
                }
                else
                {
                    // Check if this is an intermediate evolution
                    var nextEvos = tree.Forward.GetForward(evo.Species, 0);
                    foreach (var nextEvo in nextEvos.Span)
                    {
                        if (nextEvo.Species == evolvedSpecies)
                        {
                            // Found target species - use highest level requirement from chain
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
            StreamWriter errorLogger, ushort baseSpecies, byte form, string locationName, ushort locationId, byte baseLevel,
            string encounterType, bool isShinyLocked, bool isGift, string fixedBall, string version, bool isUnderground,
            PersonalTable8BDSP pt, HashSet<(ushort Species, byte Form)> processedForms)
        {
            if (pt.GetFormEntry(baseSpecies, form)?.IsPresentInGame != true)
                return;

            var nextEvolutions = TraverseEvolutions(baseSpecies, form, pt, processedForms);
            foreach (var (evoSpecies, evoForm) in nextEvolutions)
            {
                if (!processedForms.Add((evoSpecies, evoForm)) || pt.GetFormEntry(evoSpecies, evoForm)?.IsPresentInGame != true)
                    continue;

                // Get evolution level from original base species
                int evolutionMinLevel = GetMinEvolutionLevel(baseSpecies, evoSpecies);
                int minLevel = Math.Max(baseLevel, evolutionMinLevel);

                AddSingleEncounterInfo(encounterData, gameStrings, errorLogger, evoSpecies, evoForm, locationName, locationId,
                    (byte)minLevel, (byte)minLevel, encounterType, isShinyLocked, isGift, fixedBall, version, isUnderground);

                // Use evolved species as new base for next evolution
                ProcessEvolutionLine(encounterData, gameStrings, errorLogger, evoSpecies, evoForm, locationName, locationId,
                    (byte)minLevel, encounterType, isShinyLocked, isGift, fixedBall, version, isUnderground, pt, processedForms);
            }
        }

        private static void AddEncounterInfoWithEvolutions(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings,
            StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, ushort locationId, byte minLevel, byte maxLevel,
            string encounterType, bool isShinyLocked, bool isGift, string fixedBall, string version, bool isUnderground)
        {
            var pt = PersonalTable.BDSP;
            var personalInfo = pt.GetFormEntry(speciesIndex, form);

            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} form {form} not present in BDSP. Skipping.");
                return;
            }

            // Process base species
            AddSingleEncounterInfo(encounterData, gameStrings, errorLogger, speciesIndex, form, locationName, locationId,
                minLevel, maxLevel, encounterType, isShinyLocked, isGift, fixedBall, version, isUnderground);

            // Track processed species/forms to avoid duplicates
            var processedForms = new HashSet<(ushort Species, byte Form)>();
            processedForms.Add((speciesIndex, form));

            // Process all evolutions recursively
            ProcessEvolutionLine(encounterData, gameStrings, errorLogger, speciesIndex, form, locationName, locationId,
                maxLevel, encounterType, isShinyLocked, isGift, fixedBall, version, isUnderground, pt, processedForms);
        }

        private static List<(ushort Species, byte Form)> TraverseEvolutions(ushort species, byte form, PersonalTable8BDSP pt, HashSet<(ushort Species, byte Form)> processedForms)
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
            if ((version1 == "Brilliant Diamond" && version2 == "Shining Pearl") ||
                (version1 == "Shining Pearl" && version2 == "Brilliant Diamond"))
                return "Both";
            return version1; // Return existing version if they're the same
        }

        private static void ProcessStaticEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            ProcessStaticEncounterArray(Encounters8b.Encounter_BDSP, "Both", encounterData, gameStrings, errorLogger);
            ProcessStaticEncounterArray(Encounters8b.StaticBD, "Brilliant Diamond", encounterData, gameStrings, errorLogger);
            ProcessStaticEncounterArray(Encounters8b.StaticSP, "Shining Pearl", encounterData, gameStrings, errorLogger);
        }

        private static void AddSingleEncounterInfo(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings,
            StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, ushort locationId, byte minLevel, byte maxLevel,
            string encounterType, bool isShinyLocked, bool isGift, string fixedBall, string version, bool isUnderground)
        {
            string dexNumber = speciesIndex.ToString();
            if (form > 0)
                dexNumber += $"-{form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            var personalInfo = PersonalTable.BDSP.GetFormEntry(speciesIndex, form);
            string genderRatio = DetermineGenderRatio(personalInfo);

            var existingEncounter = encounterData[dexNumber].FirstOrDefault(e =>
                e.LocationId == locationId &&
                e.SpeciesIndex == speciesIndex &&
                e.Form == form &&
                e.EncounterType == encounterType &&
                e.IsUnderground == isUnderground &&
                e.Gender == genderRatio); 

            if (existingEncounter != null)
            {
                // If this is the same species in the same location, combine versions and keep lowest level
                existingEncounter.MinLevel = Math.Min(existingEncounter.MinLevel, minLevel);
                existingEncounter.MaxLevel = Math.Max(existingEncounter.MaxLevel, maxLevel);
                existingEncounter.Version = CombineVersions(existingEncounter.Version, version);

                errorLogger.WriteLine($"[{DateTime.Now}] Updated existing encounter: {gameStrings.specieslist[speciesIndex]} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {existingEncounter.MinLevel}-{existingEncounter.MaxLevel}, " +
                    $"Type: {encounterType}, Version: {existingEncounter.Version}, Gender: {genderRatio}");
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
                    IsUnderground = isUnderground,
                    IsShinyLocked = isShinyLocked,
                    IsGift = isGift,
                    FixedBall = fixedBall,
                    Version = version,
                    Gender = genderRatio
                });

                errorLogger.WriteLine($"[{DateTime.Now}] Processed new encounter: {gameStrings.specieslist[speciesIndex]} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {minLevel}-{maxLevel}, " +
                    $"Type: {encounterType}, Version: {version}, Gender: {genderRatio}");
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
            public ushort SpeciesIndex { get; set; }
            public byte Form { get; set; }
            public string LocationName { get; set; }
            public ushort LocationId { get; set; }
            public byte MinLevel { get; set; }
            public byte MaxLevel { get; set; }
            public string EncounterType { get; set; }
            public bool IsUnderground { get; set; }
            public bool IsShinyLocked { get; set; }
            public bool IsGift { get; set; }
            public string FixedBall { get; set; }
            public string Version { get; set; }
            public string Gender { get; set; } 
        }
    }
}
