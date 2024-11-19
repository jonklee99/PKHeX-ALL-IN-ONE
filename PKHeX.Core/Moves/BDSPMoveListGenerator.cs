using System;
using System.Collections.Generic;
using System.IO;

namespace PKHeX.Core.Moves
{
    public static class BDSPMoveListGenerator
    {
        public static void GenerateBDSPMovesCSV(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, true);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting CSV generation process for BDSP.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var learnSource = GameData.GetLearnSource(GameVersion.BDSP);
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource type: {learnSource?.GetType().FullName ?? "null"}");

                if (learnSource is not LearnSource8BDSP learnSource8BDSP)
                {
                    throw new InvalidOperationException($"Unable to get LearnSource for BDSP. LearnSource type: {learnSource?.GetType().FullName ?? "null"}");
                }
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource obtained successfully.");

                var pt = PersonalTable.BDSP;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for BDSP loaded.");

                using var writer = new StreamWriter(outputPath);
                writer.WriteLine("pokemon_name,dex_number,move_name,level,move_type,power,accuracy,generations,pp,category");
                errorLogger.WriteLine($"[{DateTime.Now}] CSV file header written.");

                for (ushort speciesIndex = 1; speciesIndex < pt.Table.Length; speciesIndex++)
                {
                    if (!pt.IsSpeciesInGame(speciesIndex))
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

                    var forms = FormConverter.GetFormList(speciesIndex, gameStrings.types, gameStrings.forms, ShowdownParsing.genderForms, EntityContext.Gen8b);
                    errorLogger.WriteLine($"[{DateTime.Now}] Processing species: {speciesName} (Index: {speciesIndex}, Forms: {forms.Length})");

                    for (byte form = 0; form < forms.Length; form++)
                    {
                        if (!pt.IsPresentInGame(speciesIndex, form))
                        {
                            errorLogger.WriteLine($"[{DateTime.Now}] Form {form} of species {speciesIndex} not present in BDSP. Skipping.");
                            continue;
                        }

                        if (!learnSource8BDSP.TryGetPersonal(speciesIndex, form, out var personalInfo))
                        {
                            errorLogger.WriteLine($"[{DateTime.Now}] Failed to get personal info for {speciesName} form {form}. Skipping.");
                            continue;
                        }

                        string dexNumber = speciesIndex.ToString();
                        string fullPokemonName = speciesName;
                        if (form > 0 && form < forms.Length)
                        {
                            dexNumber += $"-{form}";
                            fullPokemonName += $"-{forms[form]}";
                        }

                        var allMoves = new Dictionary<ushort, int>();
                        var eggMoves = new HashSet<ushort>();

                        // Process level-up moves
                        var learnset = learnSource8BDSP.GetLearnset(speciesIndex, form);
                        for (int i = 0; i < learnset.Moves.Length; i++)
                        {
                            var moveId = learnset.Moves[i];
                            var level = learnset.Levels[i];
                            allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, level);
                        }

                        // Get egg moves including those from pre-evolutions
                        var inheritableEggMoves = GetInheritableEggMoves(speciesIndex, form, learnSource8BDSP, pt);
                        foreach (var moveId in inheritableEggMoves)
                        {
                            allMoves[moveId] = 0; // Keep egg moves at 0
                            eggMoves.Add(moveId);
                        }

                        // Process TM moves
                        var tmMoves = LearnSource8BDSP.TMHM_BDSP;
                        for (int i = 0; i < tmMoves.Length; i++)
                        {
                            if (personalInfo.GetIsLearnTM(i))
                            {
                                var moveId = tmMoves[i];
                                allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, 1);
                            }
                        }

                        // Write all moves for this species/form
                        foreach (var move in allMoves)
                        {
                            int level = move.Value;
                            if (level == 0 && !eggMoves.Contains(move.Key))
                            {
                                level = 1; // Change 0 to 1 for non-egg moves
                            }
                            ProcessMove(move.Key, level, fullPokemonName, dexNumber, gameStrings, writer, errorLogger);
                        }
                    }
                }

                errorLogger.WriteLine($"[{DateTime.Now}] CSV file generated successfully at: {outputPath}");
            }
            catch (Exception ex)
            {
                using var errorLogger = new StreamWriter(errorLogPath, true);
                errorLogger.WriteLine($"[{DateTime.Now}] An error occurred: {ex.Message}");
                errorLogger.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private static ReadOnlySpan<ushort> GetInheritableEggMoves(ushort species, byte form, LearnSource8BDSP learnSource, PersonalTable8BDSP pt)
        {
            // Get the current species' personal info
            if (!learnSource.TryGetPersonal(species, form, out var personalInfo))
                return [];

            // Get the current species' egg moves
            var directEggMoves = learnSource.GetEggMoves(species, form);

            // If this species has no pre-evolution (HatchSpecies is 0 or equals current species)
            // then return its direct egg moves
            if (personalInfo.HatchSpecies == 0 || personalInfo.HatchSpecies == species)
                return directEggMoves;

            // Get pre-evolution's egg moves
            var baseSpecies = personalInfo.HatchSpecies;
            var baseForm = personalInfo.HatchFormIndex; 
            return learnSource.GetEggMoves(baseSpecies, baseForm);
        }

        private static void ProcessMove(ushort moveId, int level, string fullPokemonName, string dexNumber, GameStrings gameStrings, StreamWriter writer, StreamWriter errorLogger)
        {
            if (moveId > Legal.MaxMoveID_8b)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Move ID {moveId} exceeds MaxMoveID_8b. Skipping.");
                return;
            }

            var moveName = gameStrings.movelist[moveId];
            if (string.IsNullOrEmpty(moveName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty move name for ID {moveId}. Skipping.");
                return;
            }

            var moveType = gameStrings.types[MoveInfo.GetType(moveId, EntityContext.Gen8b)];
            var pp = MoveInfo.GetPP(EntityContext.Gen8b, moveId);
            var power = MoveInfo.GetPower(moveId, EntityContext.Gen8b);
            var accuracy = MoveInfo.GetAccuracy(moveId, EntityContext.Gen8b);
            var categoryByte = MoveInfo.GetCategory(moveId, EntityContext.Gen8b);
            var category = categoryByte switch
            {
                0 => "Status",
                1 => "Physical",
                2 => "Special",
                _ => "Unknown"
            };

            writer.WriteLine($"{fullPokemonName},{dexNumber},{moveName},{level},{moveType},{power},{accuracy},bdsp,{pp},{category}");
            errorLogger.WriteLine($"[{DateTime.Now}] Processed move: {moveName} for {fullPokemonName} at level {level}");
        }
    }
}
