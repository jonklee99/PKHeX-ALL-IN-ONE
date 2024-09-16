using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PKHeX.Core.Encounters
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

                // Process pickle files
                ProcessPickleFiles(encounterData, gameStrings, errorLogger);

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
            var locationName = gameStrings.GetLocationName(false, (ushort)area.Location, 8, 8, GameVersion.BDSP);
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

            string dexNumber = slot.Species.ToString();
            if (slot.Form > 0)
                dexNumber += $"-{slot.Form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            encounterData[dexNumber].Add(new EncounterInfo
            {
                SpeciesName = speciesName,
                SpeciesIndex = slot.Species,
                Form = slot.Form,
                LocationName = locationName,
                LocationId = area.Location,
                MinLevel = slot.LevelMin,
                MaxLevel = slot.LevelMax,
                EncounterType = area.Type.ToString(),
                IsUnderground = slot.IsUnderground,
                Version = version.ToString()
            });

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {area.Location}), Levels {slot.LevelMin}-{slot.LevelMax}, Type: {area.Type}");
        }

        private static void ProcessStaticEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            ProcessStaticEncounterArray(Encounters8b.Encounter_BDSP, "Both", encounterData, gameStrings, errorLogger);
            ProcessStaticEncounterArray(Encounters8b.StaticBD, "Brilliant Diamond", encounterData, gameStrings, errorLogger);
            ProcessStaticEncounterArray(Encounters8b.StaticSP, "Shining Pearl", encounterData, gameStrings, errorLogger);
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

                var locationName = gameStrings.GetLocationName(false, (ushort)encounter.Location, 8, 8, GameVersion.BDSP);
                if (string.IsNullOrEmpty(locationName))
                    locationName = $"Unknown Location {encounter.Location}";

                string dexNumber = encounter.Species.ToString();
                if (encounter.Form > 0)
                    dexNumber += $"-{encounter.Form}";

                if (!encounterData.ContainsKey(dexNumber))
                    encounterData[dexNumber] = new List<EncounterInfo>();

                encounterData[dexNumber].Add(new EncounterInfo
                {
                    SpeciesName = speciesName,
                    SpeciesIndex = encounter.Species,
                    Form = encounter.Form,
                    LocationName = locationName,
                    LocationId = encounter.Location,
                    MinLevel = encounter.Level,
                    MaxLevel = encounter.Level,
                    EncounterType = "Static",
                    IsShinyLocked = encounter.Shiny == Shiny.Never,
                    IsGift = encounter.FixedBall != Ball.None,
                    FixedBall = encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null,
                    Version = versionName
                });

                errorLogger.WriteLine($"[{DateTime.Now}] Processed static encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {encounter.Location}), Level {encounter.Level}");
            }
        }

        private static void ProcessPickleFiles(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            ProcessPickleFile("encounter_bd.pkl", encounterData, gameStrings, errorLogger, "Wild", GameVersion.BD);
            ProcessPickleFile("encounter_sp.pkl", encounterData, gameStrings, errorLogger, "Wild", GameVersion.SP);
            ProcessPickleFile("encounter_bd_underground.pkl", encounterData, gameStrings, errorLogger, "Underground", GameVersion.BD);
            ProcessPickleFile("encounter_sp_underground.pkl", encounterData, gameStrings, errorLogger, "Underground", GameVersion.SP);
        }

        private static void ProcessPickleFile(string fileName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, string encounterType, GameVersion version)
        {
            try
            {
                byte[] data = File.ReadAllBytes(fileName);
                var pt = PersonalTable.BDSP;
                for (int i = 0; i < data.Length; i += 16) // Assuming each entry is 16 bytes
                {
                    var encounter = ReadEncounter(data.AsSpan(i, 16), version);
                    var speciesIndex = encounter.Species;
                    var form = encounter.Form;
                    var personalInfo = pt[speciesIndex];
                    if (personalInfo is null || !personalInfo.IsPresentInGame)
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in BDSP. Skipping.");
                        continue;
                    }
                    var speciesName = gameStrings.specieslist[speciesIndex];
                    if (string.IsNullOrEmpty(speciesName))
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                        continue;
                    }
                    var locationId = encounter.Location;
                    var locationName = gameStrings.GetLocationName(false, (ushort)locationId, 8, 8, GameVersion.BDSP);
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
                        IsGift = encounter.FixedBall != Ball.None,
                        FixedBall = encounter.FixedBall.ToString(),
                        Version = encounter.Version.ToString()
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

        private static EncounterStatic8b ReadEncounter(ReadOnlySpan<byte> data, GameVersion version)
        {
            return new EncounterStatic8b(version)
            {
                Species = BitConverter.ToUInt16(data.Slice(0, 2)),
                Form = data[2],
                Level = data[3],
                Location = BitConverter.ToUInt16(data.Slice(4, 2)),
                Ability = (AbilityPermission)data[6],
                Shiny = (Shiny)data[7],
                FixedBall = (Ball)data[8],
                FlawlessIVCount = data[9],
                FatefulEncounter = data[10] != 0,
                // Assuming the last 5 bytes are reserved or used for other purposes
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
        }
    }
}
