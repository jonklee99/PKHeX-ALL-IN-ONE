using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;

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
                    AddEncounterInfo(encounterData, gameStrings, pt, errorLogger, slot.Species, slot.Form, locationName, locationId, slot.LevelMin, slot.LevelMax, "Wild", false, false, null, "Both", SizeType9.RANDOM);
                }
            }
        }

        private static void ProcessSevenStarRaids(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            try
            {
                byte[] data = File.ReadAllBytes("encounter_might_paldea.pkl");
                var encounters = EncounterMight9.GetArray(data);

                foreach (var encounter in encounters)
                {
                    var locationName = gameStrings.GetLocationName(false, (ushort)EncounterMight9.Location, 9, 9, GameVersion.SV);
                    if (string.IsNullOrEmpty(locationName))
                        locationName = "A Crystal Cavern";

                    AddEncounterInfo(
                        encounterData,
                        gameStrings,
                        pt,
                        errorLogger,
                        encounter.Species,
                        encounter.Form,
                        locationName,
                        EncounterMight9.Location,
                        encounter.Level,
                        encounter.Level,
                        "7-Star Raid",
                        encounter.Shiny == Shiny.Never,
                        false,
                        null,
                        "Both",
                        encounter.ScaleType,
                        encounter.Scale
                    );
                }
            }
            catch (FileNotFoundException)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Warning: File not found: encounter_might_paldea.pkl. Skipping 7-Star Raid encounters.");
            }
            catch (Exception ex)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Error processing encounter_might_paldea.pkl: {ex.Message}");
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

                AddEncounterInfo(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName, locationId, encounter.Level, encounter.Level, "Static", encounter.Shiny == Shiny.Never, false, encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null, versionName);
            }
        }

        private static void ProcessFixedEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in EncounterFixed9.GetArray(File.ReadAllBytes("encounter_fixed_paldea.pkl")))
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)encounter.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                AddEncounterInfo(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName, encounter.Location, encounter.Level, encounter.Level, "Fixed", false, false, null, "Both");
            }
        }

        private static void ProcessDistributionEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in EncounterDist9.GetArray(File.ReadAllBytes("encounter_dist_paldea.pkl")))
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)EncounterDist9.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = "Distribution Raid Den";

                AddEncounterInfo(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName, EncounterDist9.Location, encounter.Level, encounter.Level, $"Distribution Raid {encounter.Stars}★", encounter.Shiny == Shiny.Never, false, null, "Both");
            }
        }

        private static void ProcessOutbreakEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            foreach (var encounter in EncounterOutbreak9.GetArray(File.ReadAllBytes("encounter_outbreak_paldea.pkl")))
            {
                var locationName = gameStrings.GetLocationName(false, encounter.Location, 9, 9, GameVersion.SV);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                AddEncounterInfo(encounterData, gameStrings, pt, errorLogger, encounter.Species, encounter.Form, locationName, encounter.Location, encounter.LevelMin, encounter.LevelMax, "Outbreak", !encounter.IsShiny, false, null, "Both");
            }
        }

        private static void ProcessTeraRaidEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            ProcessRegionTeraRaidEncounters("encounter_gem_paldea.pkl", TeraRaidMapParent.Paldea, encounterData, gameStrings, pt, errorLogger);
            ProcessRegionTeraRaidEncounters("encounter_gem_kitakami.pkl", TeraRaidMapParent.Kitakami, encounterData, gameStrings, pt, errorLogger);
            ProcessRegionTeraRaidEncounters("encounter_gem_blueberry.pkl", TeraRaidMapParent.Blueberry, encounterData, gameStrings, pt, errorLogger);
        }

        private static void ProcessRegionTeraRaidEncounters(string fileName, TeraRaidMapParent region, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger)
        {
            try
            {
                foreach (var encounter in EncounterTera9.GetArray(File.ReadAllBytes(fileName), region))
                {
                    var locationName = gameStrings.GetLocationName(false, (ushort)EncounterTera9.Location, 9, 9, GameVersion.SV);
                    if (string.IsNullOrEmpty(locationName))
                        locationName = "Tera Raid Den";

                    AddEncounterInfo(
                        encounterData,
                        gameStrings,
                        pt,
                        errorLogger,
                        encounter.Species,
                        encounter.Form,
                        locationName,
                        EncounterTera9.Location,
                        encounter.Level,
                        encounter.Level,
                        $"Tera Raid {encounter.Stars}★",
                        encounter.Shiny == Shiny.Never,
                        false,
                        null,
                        encounter.IsAvailableHostScarlet && encounter.IsAvailableHostViolet ? "Both" : (encounter.IsAvailableHostScarlet ? "Scarlet" : "Violet"),
                        SizeType9.RANDOM
                    );
                }
            }
            catch (FileNotFoundException)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Warning: File not found: {fileName}. Skipping this region's Tera Raid encounters.");
            }
            catch (Exception ex)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Error processing {fileName}: {ex.Message}");
            }
        }

        private static void AddEncounterInfo(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, PersonalTable9SV pt, StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, int locationId, int minLevel, int maxLevel, string encounterType, bool isShinyLocked = false, bool isGift = false, string fixedBall = null, string encounterVersion = "Both", SizeType9 sizeType = SizeType9.RANDOM, byte sizeValue = 0)
        {
            var personalInfo = pt[speciesIndex, form];
            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} form {form} not present in SV. Skipping.");
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

            encounterData[dexNumber].Add(new EncounterInfo
            {
                SpeciesName = speciesName,
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
                SizeValue = sizeValue
            });

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {minLevel}-{maxLevel}, Type: {encounterType}, Size: {sizeType} {(sizeType == SizeType9.VALUE ? $"(Value: {sizeValue})" : "")}");
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
            public SizeType9 SizeType { get; set; }
            public byte SizeValue { get; set; }
        }
    }
}
