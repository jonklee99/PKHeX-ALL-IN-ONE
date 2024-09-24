using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PKHeX.Core.Encounters
{
    public static class EncounterLocationsSWSH
    {
        private const ushort MaxLair = 244; // Max Lair location ID

        public static void GenerateEncounterDataJSON(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting JSON generation process for encounters in Sword/Shield.");

                var gameStrings = GameInfo.GetStrings("en");
                var encounterData = new Dictionary<string, List<EncounterInfo>>();

                // Process regular encounter slots
                ProcessEncounterSlots(Encounters8.SlotsSW_Symbol, encounterData, gameStrings, errorLogger, "Sword Symbol");
                ProcessEncounterSlots(Encounters8.SlotsSW_Hidden, encounterData, gameStrings, errorLogger, "Sword Hidden");
                ProcessEncounterSlots(Encounters8.SlotsSH_Symbol, encounterData, gameStrings, errorLogger, "Shield Symbol");
                ProcessEncounterSlots(Encounters8.SlotsSH_Hidden, encounterData, gameStrings, errorLogger, "Shield Hidden");

                // Process static encounters
                ProcessStaticEncounters(Encounters8.StaticSWSH, "Both", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters8.StaticSW, "Sword", encounterData, gameStrings, errorLogger);
                ProcessStaticEncounters(Encounters8.StaticSH, "Shield", encounterData, gameStrings, errorLogger);

                // Process Max Lair encounters
                ProcessMaxLairEncounters(encounterData, gameStrings, errorLogger);

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

        private static void ProcessEncounterSlots(EncounterArea8[] areas, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, string slotType)
        {
            foreach (var area in areas)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)area.Location, 8, 8, GameVersion.SWSH);

                foreach (var slot in area.Slots)
                {
                    bool canGigantamax = Gigantamax.CanToggle(slot.Species, slot.Form);

                    AddEncounterInfo(encounterData, gameStrings, errorLogger, slot.Species, slot.Form, locationName, area.Location, slot.LevelMin, slot.LevelMax, $"Wild {slotType}", false, false, null, "Both", canGigantamax);
                }
            }
        }

        private static void ProcessStaticEncounters(EncounterStatic8[] encounters, string versionName, Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in encounters)
            {
                var locationName = gameStrings.GetLocationName(false, (ushort)encounter.Location, 8, 8, GameVersion.SWSH);
                bool canGigantamax = Gigantamax.CanToggle(encounter.Species, encounter.Form) || encounter.CanGigantamax;

                AddEncounterInfo(encounterData, gameStrings, errorLogger, encounter.Species, encounter.Form, locationName, encounter.Location, encounter.Level, encounter.Level, "Static", encounter.Shiny == Shiny.Never, encounter.Gift, encounter.FixedBall != Ball.None ? encounter.FixedBall.ToString() : null, versionName, canGigantamax);
            }
        }

        private static void ProcessMaxLairEncounters(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger)
        {
            foreach (var encounter in Encounters8Nest.DynAdv_SWSH)
            {
                var locationName = gameStrings.GetLocationName(false, MaxLair, 8, 8, GameVersion.SWSH);
                bool canGigantamax = Gigantamax.CanToggle(encounter.Species, encounter.Form) || encounter.CanGigantamax;

                AddEncounterInfo(encounterData, gameStrings, errorLogger, encounter.Species, encounter.Form, locationName, MaxLair, encounter.Level, encounter.Level, "Max Lair", encounter.Shiny == Shiny.Never, false, null, "Both", canGigantamax);
            }
        }

        private static void AddEncounterInfo(Dictionary<string, List<EncounterInfo>> encounterData, GameStrings gameStrings, StreamWriter errorLogger, ushort speciesIndex, byte form, string locationName, int locationId, int minLevel, int maxLevel, string encounterType, bool isShinyLocked = false, bool isGift = false, string fixedBall = null, string encounterVersion = "Both", bool canGigantamax = false)
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
                EncounterVersion = encounterVersion,
                CanGigantamax = canGigantamax
            });

            errorLogger.WriteLine($"[{DateTime.Now}] Processed encounter: {speciesName} (Dex: {dexNumber}) at {locationName} (ID: {locationId}), Levels {minLevel}-{maxLevel}, Type: {encounterType}, Can Gigantamax: {canGigantamax}");
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
            public bool CanGigantamax { get; set; }
        }
    }
}
