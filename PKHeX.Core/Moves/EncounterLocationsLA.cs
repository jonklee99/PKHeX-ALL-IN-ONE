using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PKHeX.Core.Encounters
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

                var pt = PersonalTable.LA;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for LA loaded.");

                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots
                foreach (var area in Encounters8a.SlotsLA)
                {
                    foreach (var slot in area.Slots)
                    {
                        ProcessEncounterSlot(slot, area, gameStrings, pt, encounterData, errorLogger);
                    }
                }

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

        private static void ProcessEncounterSlot(EncounterSlot8a slot, EncounterArea8a area, GameStrings gameStrings, PersonalTable8LA pt, Dictionary<string, List<EncounterInfo>> encounterData, StreamWriter errorLogger)
        {
            var speciesIndex = slot.Species;
            var form = slot.Form;

            var personalInfo = pt.GetFormEntry(speciesIndex, form);
            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in LA. Skipping.");
                return;
            }

            var speciesName = gameStrings.specieslist[speciesIndex];
            if (string.IsNullOrEmpty(speciesName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                return;
            }

            string dexNumber = speciesIndex.ToString();
            if (form > 0)
                dexNumber += $"-{form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            var locationName = gameStrings.GetLocationName(false, area.Location, 8, 8, GameVersion.PLA);
            if (string.IsNullOrEmpty(locationName))
                locationName = $"Unknown Location {area.Location}";

            encounterData[dexNumber].Add(new EncounterInfo
            {
                SpeciesName = speciesName,
                SpeciesIndex = speciesIndex,
                Form = form,
                LocationName = locationName,
                LocationId = area.Location,
                MinLevel = slot.LevelMin,
                MaxLevel = slot.LevelMax,
                EncounterType = area.Type.ToString(),
                IsAlpha = slot.IsAlpha,
                Gender = slot.Gender.ToString(),
                FlawlessIVCount = slot.FlawlessIVCount
            });

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {area.Location}), Levels {slot.LevelMin}-{slot.LevelMax}");
        }

        private static void ProcessStaticEncounters(EncounterStatic8a[] encounters, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                var speciesIndex = encounter.Species;
                var form = encounter.Form;

                var speciesName = gameStrings.specieslist[speciesIndex];
                if (string.IsNullOrEmpty(speciesName))
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                    continue;
                }

                string dexNumber = speciesIndex.ToString();
                if (form > 0)
                    dexNumber += $"-{form}";

                if (!encounterData.ContainsKey(dexNumber))
                    encounterData[dexNumber] = new List<EncounterInfo>();

                var locationName = gameStrings.GetLocationName(false, encounter.Location, 8, 8, GameVersion.PLA);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                encounterData[dexNumber].Add(new EncounterInfo
                {
                    SpeciesName = speciesName,
                    SpeciesIndex = speciesIndex,
                    Form = form,
                    LocationName = locationName,
                    LocationId = encounter.Location,
                    MinLevel = encounter.LevelMin,
                    MaxLevel = encounter.LevelMax,
                    EncounterType = "Static",
                    IsAlpha = encounter.IsAlpha,
                    Gender = ((Gender)encounter.Gender).ToString(),
                    FlawlessIVCount = encounter.FlawlessIVCount,
                    IsShiny = encounter.Shiny != Shiny.Never,
                    FixedBall = encounter.FixedBall.ToString(),
                    FatefulEncounter = encounter.FatefulEncounter
                });

                errorLogger.WriteLine($"[{DateTime.Now}] Processed static encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {encounter.Location}), Levels {encounter.LevelMin}-{encounter.LevelMax}");
            }
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
            public bool IsAlpha { get; set; }
            public string Gender { get; set; }
            public int FlawlessIVCount { get; set; }
            public bool IsShiny { get; set; }
            public string FixedBall { get; set; }
            public bool FatefulEncounter { get; set; }
        }
    }
}
