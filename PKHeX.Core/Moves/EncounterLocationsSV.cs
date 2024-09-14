using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PKHeX.Core.Encounters
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
                foreach (var area in Encounters9.Slots)
                {
                    var locationId = area.Location;
                    var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 9, 9, GameVersion.SV);
                    if (string.IsNullOrEmpty(locationName))
                        locationName = $"Unknown Location {locationId}";

                    foreach (var slot in area.Slots)
                    {
                        var speciesIndex = slot.Species;
                        var form = slot.Form;

                        var personalInfo = pt[speciesIndex];
                        if (personalInfo is null || !personalInfo.IsPresentInGame)
                        {
                            errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SV. Skipping.");
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
                            EncounterType = "Wild"
                        });

                        errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {slot.LevelMin}-{slot.LevelMax}");
                    }
                }

                // Process 7-Star Raid encounters from "a crystal cavern"
                var cavernLocationId = (int)EncounterMight9.Location; // Location ID for "a crystal cavern"
                var cavernLocationName = gameStrings.GetLocationName(false, (ushort)cavernLocationId, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(cavernLocationName))
                    cavernLocationName = "A Crystal Cavern";

                foreach (var encounter in Encounters9.Might)
                {
                    var speciesIndex = encounter.Species;
                    var form = encounter.Form;

                    var personalInfo = pt[speciesIndex];
                    if (personalInfo is null || !personalInfo.IsPresentInGame)
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SV. Skipping.");
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
                        LocationName = cavernLocationName,
                        LocationId = cavernLocationId,
                        MinLevel = encounter.Level,
                        MaxLevel = encounter.Level,
                        EncounterType = "7-Star Raid"
                    });

                    errorLogger.WriteLine($"[{DateTime.Now}] Processed 7-Star Raid encounter: {speciesName} (Dex: {dexNumber}) at {cavernLocationName} (ID: {cavernLocationId}), Level {encounter.Level}");
                }

                // Process static encounters for both versions
                ProcessStaticEncounters(Encounters9.Encounter_SV, "Both", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters9.StaticSL, "Scarlet", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters9.StaticVL, "Violet", encounterData, gameStrings, errorLogger);

                // Process trade encounters
                foreach (var encounter in Encounters9.TradeGift_SV)
                {
                    var speciesIndex = encounter.Species;
                    var form = encounter.Form;

                    var personalInfo = pt[speciesIndex];
                    if (personalInfo is null || !personalInfo.IsPresentInGame)
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SV. Skipping.");
                        continue;
                    }

                    var speciesName = gameStrings.specieslist[speciesIndex];
                    if (string.IsNullOrEmpty(speciesName))
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                        continue;
                    }

                    var locationName = "In-Game Trade";
                    var locationId = -1; // Arbitrary ID for trades

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
                        EncounterType = "Trade",
                        IsShinyLocked = encounter.Shiny == Shiny.Never,
                        IsGift = true,
                        FixedBall = encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null
                    });

                    errorLogger.WriteLine($"[{DateTime.Now}] Processed trade encounter: {speciesName} (Dex: {dexNumber}) via trade at Level {encounter.Level}");
                }

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

        private static void ProcessStaticEncounters(EncounterStatic9[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var pt = PersonalTable.SV; // Access the PersonalTable9SV instance
            foreach (var encounter in encounters)
            {
                var speciesIndex = encounter.Species;
                var form = encounter.Form;

                var personalInfo = pt[speciesIndex];
                if (personalInfo is null || !personalInfo.IsPresentInGame)
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SV. Skipping.");
                    continue;
                }

                var speciesName = gameStrings.specieslist[speciesIndex];
                if (string.IsNullOrEmpty(speciesName))
                {
                    errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                    continue;
                }

                var locationId = encounter.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 9, 9, GameVersion.SV);
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
                    IsGift = false, // Set to true if appropriate
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
            public string EncounterVersion { get; set; } // "Scarlet", "Violet", or "Both"
        }
    }
}
