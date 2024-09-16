using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Diagnostics.Metrics;

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

                // Process Max Lair (Underground) Encounters
                ProcessUndergroundEncounters(encounterData, gameStrings, errorLogger);

                // Process encounters from pickle files
                ProcessPickleFileEncounters(encounterData, gameStrings, errorLogger);

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

        private static void ProcessUndergroundEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            try
            {
                // Read the Max Lair encounters from the .pkl file
                byte[] data = File.ReadAllBytes("encounter_swsh_underground.pkl");

                var pt = PersonalTable.SWSH;

                for (int i = 0; i < data.Length; i += 14) // Each entry is 14 bytes
                {
                    var encounter = EncounterStatic8U.Read(data.AsSpan(i, 14));

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

                    var locationId = EncounterStatic8U.Location; // Use the constant from EncounterStatic8U
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
                        EncounterType = "Max Lair",
                        IsShinyLocked = !encounter.IsShinyXorValid(0), // Adjust based on shiny mechanics
                        IsGift = false,
                        FixedBall = null,
                        EncounterVersion = "Both"
                    });

                    errorLogger.WriteLine($"[{DateTime.Now}] Processed Max Lair encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Level {encounter.Level}");
                }
            }
            catch (FileNotFoundException)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Warning: File not found: encounter_swsh_underground.pkl. Skipping Max Lair encounters.");
            }
            catch (Exception ex)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Error processing encounter_swsh_underground.pkl: {ex.Message}");
            }
        }

        private static void ProcessPickleFileEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            ProcessPickleFile("encounter_sw_dist.pkl", encounterData, gameStrings, errorLogger, "Sword Distribution", GameVersion.SW);
            ProcessPickleFile("encounter_sh_dist.pkl", encounterData, gameStrings, errorLogger, "Shield Distribution", GameVersion.SH);
            ProcessPickleFile("encounter_sw_nest.pkl", encounterData, gameStrings, errorLogger, "Sword Nest", GameVersion.SW);
            ProcessPickleFile("encounter_sh_nest.pkl", encounterData, gameStrings, errorLogger, "Shield Nest", GameVersion.SH);
            ProcessPickleFile("encounter_sw_hidden.pkl", encounterData, gameStrings, errorLogger, "Sword Hidden", GameVersion.SW);
            ProcessPickleFile("encounter_sh_hidden.pkl", encounterData, gameStrings, errorLogger, "Shield Hidden", GameVersion.SH);
            ProcessPickleFile("encounter_sw_symbol.pkl", encounterData, gameStrings, errorLogger, "Sword Symbol", GameVersion.SW);
            ProcessPickleFile("encounter_sh_symbol.pkl", encounterData, gameStrings, errorLogger, "Shield Symbol", GameVersion.SH);
        }

        private static void ProcessPickleFile(string fileName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, string encounterType, GameVersion version)
        {
            try
            {
                byte[] data = File.ReadAllBytes(fileName);
                var pt = PersonalTable.SWSH;

                for (int i = 0; i < data.Length; i += 16) // Assuming each entry is 16 bytes
                {
                    var encounter = ReadEncounter(data.AsSpan(i, 16), version);

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
                        EncounterType = encounterType,
                        IsShinyLocked = encounter.Shiny == Shiny.Never,
                        IsGift = encounter.Gift,
                        FixedBall = encounter.FixedBall.ToString(),
                        EncounterVersion = encounter.Version.ToString()
                    });

                    errorLogger.WriteLine($"[{DateTime.Now}] Processed {encounterType} encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Level {encounter.Level}");
                }
            }
            catch (FileNotFoundException)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Warning: File not found: {fileName}. Skipping these encounters.");
            }
            catch (Exception ex)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Error processing {fileName}: {ex.Message}");
            }
        }

        private static EncounterStatic8 ReadEncounter(ReadOnlySpan<byte> data, GameVersion version)
        {
            return new EncounterStatic8(version)
            {
                Species = BitConverter.ToUInt16(data.Slice(0, 2)),
                Form = data[2],
                Level = data[3],
                Location = BitConverter.ToUInt16(data.Slice(4, 2)),
                Ability = (AbilityPermission)data[6],
                Shiny = (Shiny)data[7],
                FixedBall = (Ball)data[8],
                Nature = (Nature)data[9],
                Gender = data[10],
                FlawlessIVCount = data[11],
                DynamaxLevel = data[12],
                CanGigantamax = data[13] != 0,
                Moves = new Moveset(
                    BitConverter.ToUInt16(data.Slice(14, 2)),
                    BitConverter.ToUInt16(data.Slice(16, 2)),
                    BitConverter.ToUInt16(data.Slice(18, 2)),
                    BitConverter.ToUInt16(data.Slice(20, 2))
                )
            };
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
                    AddEncounterInfo(encounterData, gameStrings, errorLogger, slot.Species, slot.Form, locationName, locationId, slot.LevelMin, slot.LevelMax, $"Wild {slotType}", false, false, null, "Both");
                }
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic8[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                var locationId = encounter.Location;
                var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 8, 8, GameVersion.SWSH);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {locationId}";

                AddEncounterInfo(encounterData, gameStrings, errorLogger, encounter.Species, encounter.Form, locationName, locationId, encounter.Level, encounter.Level, "Static", encounter.Shiny == Shiny.Never, encounter.Gift, encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null, versionName);
            }
        }

        private static void AddEncounterInfo(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, int locationId, int minLevel, int maxLevel, string encounterType, bool isShinyLocked = false, bool isGift = false, string fixedBall = null, string encounterVersion = "Both")
        {
            var pt = PersonalTable.SWSH;
            var personalInfo = pt[speciesIndex];
            if (personalInfo is null || !personalInfo.IsPresentInGame)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in SWSH. Skipping.");
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
                EncounterVersion = encounterVersion
            });

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {minLevel}-{maxLevel}, Type: {encounterType}");
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
