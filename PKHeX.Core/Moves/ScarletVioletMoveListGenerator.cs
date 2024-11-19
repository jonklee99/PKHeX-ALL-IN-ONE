using System;
using System.IO;
using System.Collections.Generic;

namespace PKHeX.Core.Moves
{
    public static class ScarletVioletMoveListGenerator
    {
        public static void GenerateScarletVioletMovesCSV(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, true);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting CSV generation process for Scarlet/Violet.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var learnSource = GameData.GetLearnSource(GameVersion.SV);
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource type: {learnSource?.GetType().FullName ?? "null"}");

                if (learnSource is not LearnSource9SV learnSource9SV)
                {
                    throw new InvalidOperationException($"Unable to get LearnSource for Scarlet/Violet. LearnSource type: {learnSource?.GetType().FullName ?? "null"}");
                }
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource obtained successfully.");

                var pt = PersonalTable.SV;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for SV loaded.");

                using var writer = new StreamWriter(outputPath);
                writer.WriteLine("pokemon_name,dex_number,move_name,level,move_type,power,accuracy,generations,pp,category");
                errorLogger.WriteLine($"[{DateTime.Now}] CSV file header written.");

                for (ushort speciesIndex = 1; speciesIndex <= Legal.MaxSpeciesID_9; speciesIndex++)
                {
                    if (!pt.IsSpeciesInGame(speciesIndex))
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

                    var forms = FormConverter.GetFormList(speciesIndex, gameStrings.types, gameStrings.forms, ShowdownParsing.genderForms, EntityContext.Gen9);
                    errorLogger.WriteLine($"[{DateTime.Now}] Processing species: {speciesName} (Index: {speciesIndex}, Forms: {forms.Length})");

                    for (byte form = 0; form < forms.Length; form++)
                    {
                        if (!pt.IsPresentInGame(speciesIndex, form))
                        {
                            errorLogger.WriteLine($"[{DateTime.Now}] Form {form} of species {speciesIndex} not present in SV. Skipping.");
                            continue;
                        }

                        if (!learnSource9SV.TryGetPersonal(speciesIndex, form, out var personalInfo))
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

                        var evo = new EvoCriteria
                        {
                            Species = speciesIndex,
                            Form = form,
                            LevelMax = 100,
                        };

                        var learnset = learnSource9SV.GetLearnset(speciesIndex, form);
                        var reminderMoves = learnSource9SV.GetReminderMoves(speciesIndex, form);
                        var tmMoves = personalInfo.RecordPermitIndexes;

                        var allMoves = new Dictionary<ushort, int>();

                        // Process level-up moves
                        foreach (var moveId in learnset.GetMoveRange(evo.LevelMax))
                        {
                            var level = learnset.GetLevelLearnMove(moveId);
                            allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, level);
                        }

                        // Get egg moves from base form
                        var baseFormEggMoves = GetInheritableEggMoves(speciesIndex, form, learnSource9SV, pt);
                        foreach (var moveId in baseFormEggMoves)
                        {
                            allMoves[moveId] = 0; // Egg moves are level 0
                        }

                        // Process reminder moves
                        foreach (var moveId in reminderMoves)
                        {
                            allMoves[moveId] = allMoves.ContainsKey(moveId) ? allMoves[moveId] : 1;
                        }

                        // Process TM moves
                        for (int i = 0; i < tmMoves.Length; i++)
                        {
                            if (personalInfo.GetIsLearnTM(i))
                            {
                                var moveId = tmMoves[i];
                                allMoves[moveId] = allMoves.ContainsKey(moveId) ? allMoves[moveId] : 1;
                            }
                        }

                        // Write all moves for this species/form
                        foreach (var move in allMoves)
                        {
                            ProcessMove(move.Key, move.Value, dexNumber, fullPokemonName, gameStrings, writer, errorLogger);
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

        private static ReadOnlySpan<ushort> GetInheritableEggMoves(ushort species, byte form, LearnSource9SV learnSource, PersonalTable9SV pt)
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
            var baseForm = personalInfo.HatchFormIndexEverstone;
            return learnSource.GetEggMoves(baseSpecies, baseForm);
        }

        private static void ProcessMove(ushort moveId, int level, string dexNumber, string fullPokemonName, GameStrings gameStrings, StreamWriter writer, StreamWriter errorLogger)
        {
            if (moveId > Legal.MaxMoveID_9)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Move ID {moveId} exceeds MaxMoveID_9. Skipping.");
                return;
            }

            var moveName = gameStrings.movelist[moveId];
            if (string.IsNullOrEmpty(moveName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty move name for ID {moveId}. Skipping.");
                return;
            }

            var moveType = gameStrings.types[MoveInfo.GetType(moveId, EntityContext.Gen9)];
            var pp = MoveInfo.GetPP(EntityContext.Gen9, moveId);
            var power = MoveInfo.GetPower(moveId, EntityContext.Gen9);
            var accuracy = MoveInfo.GetAccuracy(moveId, EntityContext.Gen9);
            var categoryByte = MoveInfo.GetCategory(moveId, EntityContext.Gen9);
            var category = categoryByte switch
            {
                0 => "Status",
                1 => "Physical",
                2 => "Special",
                _ => "Unknown"
            };

            writer.WriteLine($"{fullPokemonName},{dexNumber},{moveName},{level},{moveType},{power},{accuracy},9,{pp},{category}");
            errorLogger.WriteLine($"[{DateTime.Now}] Processed move: {moveName} for {fullPokemonName} at level {level}");
        }
    }
}
