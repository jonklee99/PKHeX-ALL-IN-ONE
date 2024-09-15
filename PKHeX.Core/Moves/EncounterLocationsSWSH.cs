using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PKHeX.Core.Encounters
{
    public static class EncounterLocationsSWSH
    {
        public static void GenerateEncounterDataJSON(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting JSON generation process for encounters in Sword/Shield.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var pt = PersonalTable.SWSH;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for SWSH loaded.");

                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots for Sword
                ProcessEncounterSlots(Encounters8.SlotsSW_Symbol, encounterData, gameStrings, errorLogger, "Sword Symbol");
                ProcessEncounterSlots(Encounters8.SlotsSW_Hidden, encounterData, gameStrings, errorLogger, "Sword Hidden");

                // Process regular encounter slots for Shield
                ProcessEncounterSlots(Encounters8.SlotsSH_Symbol, encounterData, gameStrings, errorLogger, "Shield Symbol");
                ProcessEncounterSlots(Encounters8.SlotsSH_Hidden, encounterData, gameStrings, errorLogger, "Shield Hidden");

                // Process static encounters for both versions
                ProcessStaticEncounters(Encounters8.StaticSWSH, "Both", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters8.StaticSW, "Sword", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters8.StaticSH, "Shield", encounterData, gameStrings, errorLogger);

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

        private static void ProcessEncounterSlots(EncounterArea8[] areas, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, string slotType)
        {
            foreach (var area in areas)
            {
                var locationId = area.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 8, 8, GameVersion.SWSH);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {locationId}";

                foreach (var slot in area.Slots)
                {
                    var speciesIndex = slot.Species;
                    var form = slot.Form;

                    var personalInfo = PersonalTable.SWSH[speciesIndex];
                    if (personalInfo is null || !personalInfo.IsPresentInGame)
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SWSH. Skipping.");
                        continue;
                    }

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

                    encounterData[dexNumber].Add(new EncounterInfo
                    {
                        SpeciesName = speciesName,
                        SpeciesIndex = speciesIndex,
                        Form = form,
                        LocationName = locationName,
                        LocationId = locationId,
                        MinLevel = slot.LevelMin,
                        MaxLevel = slot.LevelMax,
                        EncounterType = $"Wild {slotType}"
                    });

                    errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {slot.LevelMin}-{slot.LevelMax}, Type: {slotType}");
                }
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic8[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var pt = PersonalTable.SWSH; // Access the PersonalTable8SWSH instance
            foreach (var encounter in encounters)
            {
                var speciesIndex = encounter.Species;
                var form = encounter.Form;

                var personalInfo = pt[speciesIndex];
                if (personalInfo is null || !personalInfo.IsPresentInGame)
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SWSH. Skipping.");
                    continue;
                }

                var speciesName = gameStrings.specieslist[speciesIndex];
                if (string.IsNullOrEmpty(speciesName))
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                    continue;
                }

                var locationId = encounter.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 8, 8, GameVersion.SWSH);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {locationId}";

                string dexNumber = speciesIndex.ToString();
                if (form > 0)
                    dexNumber += $"-{form}";

                if (!encounterData.ContainsKey(dexNumber))
                    encounterData[dexNumber] = new List<EncounterInfo>();

                encounterData[dexNumber].Add(new EncounterInfo
                {
                    SpeciesName = speciesName,
                    SpeciesIndex = speciesIndex,
                    Form = form,
                    LocationName = locationName,
                    LocationId = locationId,
                    MinLevel = encounter.Level,
                    MaxLevel = encounter.Level,
                    EncounterType = "Static",
                    IsShinyLocked = encounter.Shiny == Shiny.Never,
                    IsGift = encounter.Gift,
                    FixedBall = encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null,
                    EncounterVersion = versionName
                });

                errorLogger.WriteLine($"[{DateTime.Now}] Processed static encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Level {encounter.Level}");
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
            public bool IsShinyLocked { get; set; }
            public bool IsGift { get; set; }
            public string FixedBall { get; set; }
            public string EncounterVersion { get; set; } // "Sword", "Shield", or "Both"
        }
    }
}
