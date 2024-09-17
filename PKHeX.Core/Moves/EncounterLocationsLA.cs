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
                    AddEncounterInfo(slot, area.Location, area.Type.ToString(), encounterData, gameStrings, errorLogger);
                }
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic8a[] encounters, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                AddEncounterInfo(encounter, encounter.Location, "Static", encounterData, gameStrings, errorLogger);
            }
        }

        private static void AddEncounterInfo(ISpeciesForm encounter, ushort locationId, string encounterType, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            var speciesIndex = encounter.Species;
            var form = encounter.Form;

            var speciesName = gameStrings.specieslist[speciesIndex];
            if (string.IsNullOrEmpty(speciesName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                return;
            }

            var locationName = gameStrings.GetLocationName(false, (byte)(locationId & 0xFF), 8, 8, GameVersion.PLA);
            if (string.IsNullOrEmpty(locationName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Unknown location ID: {locationId} for species {speciesName} (Index: {speciesIndex}, Form: {form}). Skipping this encounter.");
                return;
            }

            string dexNumber = speciesIndex.ToString();
            if (form > 0)
                dexNumber += $"-{form}";

            if (!encounterData.ContainsKey(dexNumber))
                encounterData[dexNumber] = new List<EncounterInfo>();

            var info = new EncounterInfo
            {
                SpeciesName = speciesName,
                SpeciesIndex = speciesIndex,
                Form = form,
                LocationName = locationName,
                LocationId = locationId,
                EncounterType = encounterType,
            };

            if (encounter is EncounterSlot8a slot)
            {
                info.MinLevel = slot.LevelMin;
                info.MaxLevel = slot.LevelMax;
                info.IsAlpha = slot.IsAlpha;
                info.Gender = slot.Gender.ToString();
                info.FlawlessIVCount = slot.FlawlessIVCount;
            }
            else if (encounter is EncounterStatic8a static8a)
            {
                info.MinLevel = static8a.LevelMin;
                info.MaxLevel = static8a.LevelMax;
                info.IsAlpha = static8a.IsAlpha;
                info.Gender = ((Gender)static8a.Gender).ToString();
                info.FlawlessIVCount = static8a.FlawlessIVCount;
                info.IsShiny = static8a.Shiny != Shiny.Never;
                info.FixedBall = static8a.FixedBall.ToString();
                info.FatefulEncounter = static8a.FatefulEncounter;
            }

            encounterData[dexNumber].Add(info);

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Type: {encounterType}");
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
