using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace PKHeX.Core.MetLocationGenerator
{
    public static class EncounterLocationsLA
    {
        public static void GenerateEncounterDataJSON(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting JSON generation process for encounters in Legends Arceus.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots
                ProcessEncounterSlots(Encounters8a.SlotsLA, encounterData, gameStrings, errorLogger);

                // Process static encounters
                ProcessStaticEncounters(Encounters8a.StaticLA, encounterData, gameStrings, errorLogger);

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

        private static void ProcessEncounterSlots(EncounterArea8a[] areas, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var area in areas)
            {
                foreach (var slot in area.Slots)
                {
                    AddEncounterInfoWithEvolutions(slot, area.Location, area.Type.ToString(), encounterData, gameStrings, errorLogger);
                }
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic8a[] encounters, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                AddEncounterInfoWithEvolutions(encounter, encounter.Location, "Static", encounterData, gameStrings, errorLogger);
            }
        }

        private static int GetMinEvolutionLevel(ushort baseSpecies, ushort evolvedSpecies)
        {
            var tree = EvolutionTree.Evolves8a;
            var pk = new PA8 { Species = baseSpecies, Form = 0, CurrentLevel = 100, Version = GameVersion.PLA };
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

        private static void AddEncounterInfoWithEvolutions(ISpeciesForm encounter, ushort locationId, string encounterType,
            Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var speciesIndex = encounter.Species;
            var form = encounter.Form;
            var pt = PersonalTable.LA;
            var personalInfo = pt.GetFormEntry(speciesIndex, form);

            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} form {form} not present in LA. Skipping.");
                return;
            }

            var locationName = gameStrings.GetLocationName(false, (byte)(locationId & 0xFF), 8, 8, GameVersion.PLA);
            if (string.IsNullOrEmpty(locationName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Unknown location ID: {locationId} for species {gameStrings.specieslist[speciesIndex]} (Index: {speciesIndex}, Form: {form}). Skipping this encounter.");
                return;
            }

            // Process base species
            AddSingleEncounterInfo(encounter, locationId, locationName, encounterType, encounterData, gameStrings, errorLogger);

            // Track processed species/forms to avoid duplicates
            var processedForms = new HashSet<(ushort Species, byte Form)>();
            processedForms.Add((speciesIndex, form));

            // Process all evolutions recursively
            ProcessEvolutionLine(encounter, locationId, locationName, encounterType, encounterData, gameStrings, errorLogger,
                speciesIndex, form, pt, processedForms);
        }

        private static void ProcessEvolutionLine(ISpeciesForm baseEncounter, ushort locationId, string locationName, string encounterType,
            Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger,
            ushort species, byte form, PersonalTable8LA pt, HashSet<(ushort Species, byte Form)> processedForms)
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

                // Get base encounter level
                int baseLevel = baseEncounter switch
                {
                    EncounterSlot8a slot => slot.LevelMin,
                    EncounterStatic8a static8a => static8a.LevelMin,
                    _ => 1
                };

                // Get minimum evolution level
                var evolutionMinLevel = GetMinEvolutionLevel(species, evoSpecies);
                // Use the higher of the evolution requirement and base encounter level
                var minLevel = Math.Max(baseLevel, evolutionMinLevel);

                // Create evolution encounter using base encounter's properties and new level
                var evoEncounter = CreateEvolvedEncounter(baseEncounter, evoSpecies, evoForm, minLevel);

                // Add the evolved form with inherited properties and adjusted level
                AddSingleEncounterInfo(evoEncounter, locationId, locationName, encounterType, encounterData, gameStrings, errorLogger);

                // Recursively process next evolutions
                ProcessEvolutionLine(evoEncounter, locationId, locationName, encounterType, encounterData, gameStrings, errorLogger,
                    evoSpecies, evoForm, pt, processedForms);
            }
        }

        private static ISpeciesForm CreateEvolvedEncounter(ISpeciesForm baseEncounter, ushort evoSpecies, byte evoForm, int minLevel)
        {
            // Handle both slot and static encounters
            if (baseEncounter is EncounterSlot8a slot)
            {
                return new EncounterSlot8a(
                    slot.Parent,
                    evoSpecies,
                    evoForm,
                    (byte)minLevel,
                    (byte)minLevel, // Use same level for min/max for evolved forms
                    slot.FlawlessIVCount,
                    slot.AlphaType,
                    slot.Gender);
            }
            else if (baseEncounter is EncounterStatic8a static8a)
            {
                return new EncounterStatic8a(
                    evoSpecies,
                    evoForm,
                    (byte)minLevel,
                    (byte)minLevel,
                    static8a.FlawlessIVCount)
                {
                    Location = static8a.Location,
                    Shiny = static8a.Shiny,
                    Gender = static8a.Gender,
                    IsAlpha = static8a.IsAlpha,
                    FixedBall = static8a.FixedBall,
                    FatefulEncounter = static8a.FatefulEncounter
                };
            }

            return baseEncounter;
        }

        private static List<(ushort Species, byte Form)> TraverseEvolutions(ushort species, byte form, PersonalTable8LA pt, HashSet<(ushort Species, byte Form)> processedForms)
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

        private static void AddSingleEncounterInfo(ISpeciesForm encounter, ushort locationId, string locationName, string encounterType,
            Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            string dexNumber = encounter.Species.ToString();
            if (encounter.Form > 0)
                dexNumber += $"-{encounter.Form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            // Get personal info for proper gender ratio
            var personalInfo = PersonalTable.LA.GetFormEntry(encounter.Species, encounter.Form);
            string genderRatio = DetermineGenderRatio(personalInfo);

            // Check for existing encounter with updated gender comparison
            var existingEncounter = encounterData[dexNumber].FirstOrDefault(e =>
                e.LocationId == locationId &&
                e.SpeciesIndex == encounter.Species &&
                e.Form == encounter.Form &&
                e.EncounterType == encounterType &&
                e.IsAlpha == (encounter is EncounterSlot8a slot ? slot.IsAlpha :
                    encounter is EncounterStatic8a static8a && static8a.IsAlpha) &&
                e.Gender == genderRatio); // Add gender to comparison

            if (existingEncounter != null)
            {
                // Update existing encounter with lowest level
                if (encounter is EncounterSlot8a newSlot)
                {
                    existingEncounter.MinLevel = Math.Min(existingEncounter.MinLevel, newSlot.LevelMin);
                    existingEncounter.MaxLevel = Math.Max(existingEncounter.MaxLevel, newSlot.LevelMax);
                }
                else if (encounter is EncounterStatic8a newStatic)
                {
                    existingEncounter.MinLevel = Math.Min(existingEncounter.MinLevel, newStatic.LevelMin);
                    existingEncounter.MaxLevel = Math.Max(existingEncounter.MaxLevel, newStatic.LevelMax);
                }

                errorLogger.WriteLine($"[{DateTime.Now}] Updated existing encounter: {existingEncounter.SpeciesName} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {existingEncounter.MinLevel}-{existingEncounter.MaxLevel}");
            }
            else
            {
                // Add new encounter with proper gender info
                var info = new EncounterInfo
                {
                    SpeciesName = gameStrings.specieslist[encounter.Species],
                    SpeciesIndex = encounter.Species,
                    Form = encounter.Form,
                    LocationName = locationName,
                    LocationId = locationId,
                    EncounterType = encounterType,
                    Gender = genderRatio // Use determined gender ratio
                };

                if (encounter is EncounterSlot8a slot)
                {
                    info.MinLevel = slot.LevelMin;
                    info.MaxLevel = slot.LevelMax;
                    info.IsAlpha = slot.IsAlpha;
                    info.FlawlessIVCount = slot.FlawlessIVCount;
                }
                else if (encounter is EncounterStatic8a static8a)
                {
                    info.MinLevel = static8a.LevelMin;
                    info.MaxLevel = static8a.LevelMax;
                    info.IsAlpha = static8a.IsAlpha;
                    info.FlawlessIVCount = static8a.FlawlessIVCount;
                    info.IsShiny = static8a.Shiny != Shiny.Never;
                    info.FixedBall = static8a.FixedBall.ToString();
                    info.FatefulEncounter = static8a.FatefulEncounter;
                }

                encounterData[dexNumber].Add(info);
                errorLogger.WriteLine($"[{DateTime.Now}] Processed new encounter: {info.SpeciesName} " +
                    $"(Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {info.MinLevel}-{info.MaxLevel}, Type: {encounterType}, Gender: {info.Gender}");
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
                _ => "Male, Female"         // Mixed gender ratio
            };
        }

        private class EncounterInfo
        {
            public string? SpeciesName { get; set; }
            public int SpeciesIndex { get; set; }
            public int Form { get; set; }
            public string? LocationName { get; set; }
            public int LocationId { get; set; }
            public int MinLevel { get; set; }
            public int MaxLevel { get; set; }
            public string? EncounterType { get; set; }
            public bool IsAlpha { get; set; }
            public string? Gender { get; set; } 
            public int FlawlessIVCount { get; set; }
            public bool IsShiny { get; set; }
            public string? FixedBall { get; set; }
            public bool FatefulEncounter { get; set; }
        }
    }
}
