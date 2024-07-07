using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PKHeX.Core.Moves
{
    public static class SWSHMoveListGenerator
    {
        public static void GenerateSWSHMovesCSV(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, true);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting CSV generation process for SWSH.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var learnSource = GameData.GetLearnSource(GameVersion.SWSH);
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource type: {learnSource?.GetType().FullName ?? "null"}");

                if (learnSource is not LearnSource8SWSH learnSource8SWSH)
                {
                    throw new InvalidOperationException($"Unable to get LearnSource for SWSH. LearnSource type: {learnSource?.GetType().FullName ?? "null"}");
                }
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource obtained successfully.");

                using var writer = new StreamWriter(outputPath);
                writer.WriteLine("pokemon_name,dex_number,move_name,level,move_type,power,accuracy,generations,pp,category");
                errorLogger.WriteLine($"[{DateTime.Now}] CSV file header written.");

                for (ushort speciesIndex = 1; speciesIndex <= Legal.MaxSpeciesID_8; speciesIndex++)
                {
                    var speciesName = gameStrings.specieslist[speciesIndex];
                    if (string.IsNullOrEmpty(speciesName))
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                        continue;
                    }

                    var forms = FormConverter.GetFormList(speciesIndex, gameStrings.types, gameStrings.forms, ShowdownParsing.genderForms, EntityContext.Gen8);
                    errorLogger.WriteLine($"[{DateTime.Now}] Processing species: {speciesName} (Index: {speciesIndex}, Forms: {forms.Length})");

                    for (byte form = 0; form < forms.Length; form++)
                    {
                        if (!learnSource8SWSH.TryGetPersonal(speciesIndex, form, out var personalInfo))
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

                        // Process level-up moves
                        var learnset = learnSource8SWSH.GetLearnset(speciesIndex, form);
                        for (int i = 0; i < learnset.Moves.Length; i++)
                        {
                            var moveId = learnset.Moves[i];
                            var level = learnset.Levels[i];
                            allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, level);
                        }

                        // Process egg moves
                        foreach (var moveId in learnSource8SWSH.GetEggMoves(speciesIndex, form))
                        {
                            allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, 0);
                        }

                        // Process TR moves
                        var trMoves = personalInfo.RecordPermitIndexes;
                        for (int i = 0; i < trMoves.Length; i++)
                        {
                            if (personalInfo.GetIsLearnTR(i))
                            {
                                var moveId = trMoves[i];
                                allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, 0);
                            }
                        }

                        // Write all moves for this species/form
                        foreach (var move in allMoves)
                        {
                            ProcessMove(move.Key, move.Value, fullPokemonName, dexNumber, gameStrings, writer, errorLogger);
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
                throw; // Re-throw the exception after logging
            }
        }

        private static void ProcessMove(ushort moveId, int level, string fullPokemonName, string dexNumber, GameStrings gameStrings, StreamWriter writer, StreamWriter errorLogger)
        {
            if (moveId > Legal.MaxMoveID_8)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Move ID {moveId} exceeds MaxMoveID_8. Skipping.");
                return;
            }

            var moveName = gameStrings.movelist[moveId];
            if (string.IsNullOrEmpty(moveName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty move name for ID {moveId}. Skipping.");
                return;
            }

            var moveType = gameStrings.types[MoveInfo.GetType(moveId, EntityContext.Gen8)];
            var pp = MoveInfo.GetPP(EntityContext.Gen8, moveId);
            var power = MoveInfo.GetPower(moveId, EntityContext.Gen8);
            var accuracy = MoveInfo.GetAccuracy(moveId, EntityContext.Gen8);
            var categoryByte = MoveInfo.GetCategory(moveId, EntityContext.Gen8);
            var category = categoryByte switch
            {
                0 => "Status",
                1 => "Physical",
                2 => "Special",
                _ => "Unknown"
            };

            writer.WriteLine($"{fullPokemonName},{dexNumber},{moveName},{level},{moveType},{power},{accuracy},swsh,{pp},{category}");
            errorLogger.WriteLine($"[{DateTime.Now}] Processed move: {moveName} for {fullPokemonName} at level {level}");
        }
    }
}
