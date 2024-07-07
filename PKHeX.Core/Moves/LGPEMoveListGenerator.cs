using System;
using System.Collections.Generic;
using System.IO;

namespace PKHeX.Core.Moves
{
    public static class LGPEMoveListGenerator
    {
        public static void GenerateLGPEMovesCSV(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, true);
                errorLogger.WriteLine($"[{DateTime.Now}] Starting CSV generation process for LGPE.");

                var gameStrings = GameInfo.GetStrings("en");
                errorLogger.WriteLine($"[{DateTime.Now}] Game strings loaded.");

                var learnSource = GameData.GetLearnSource(GameVersion.GG);
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource type: {learnSource?.GetType().FullName ?? "null"}");

                if (learnSource is not LearnSource7GG learnSource7GG)
                {
                    throw new InvalidOperationException($"Unable to get LearnSource for LGPE. LearnSource type: {learnSource?.GetType().FullName ?? "null"}");
                }
                errorLogger.WriteLine($"[{DateTime.Now}] LearnSource obtained successfully.");

                var pt = PersonalTable.GG;
                errorLogger.WriteLine($"[{DateTime.Now}] PersonalTable for LGPE loaded.");

                using var writer = new StreamWriter(outputPath);
                writer.WriteLine("pokemon_name,dex_number,move_name,level,move_type,power,accuracy,generations,pp,category");
                errorLogger.WriteLine($"[{DateTime.Now}] CSV file header written.");

                for (ushort speciesIndex = 1; speciesIndex < pt.Table.Length; speciesIndex++)
                {
                    // Check if the species is present in LGPE
                    if (!pt.IsSpeciesInGame(speciesIndex))
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Species {speciesIndex} not present in LGPE. Skipping.");
                        continue;
                    }

                    var speciesName = gameStrings.specieslist[speciesIndex];
                    if (string.IsNullOrEmpty(speciesName))
                    {
                        errorLogger.WriteLine($"[{DateTime.Now}] Empty species name for index {speciesIndex}. Skipping.");
                        continue;
                    }

                    var forms = FormConverter.GetFormList(speciesIndex, gameStrings.types, gameStrings.forms, ShowdownParsing.genderForms, EntityContext.Gen7b);
                    errorLogger.WriteLine($"[{DateTime.Now}] Processing species: {speciesName} (Index: {speciesIndex}, Forms: {forms.Length})");

                    for (byte form = 0; form < forms.Length; form++)
                    {
                        // Check if this specific form is present in the game
                        if (!pt.IsPresentInGame(speciesIndex, form))
                        {
                            errorLogger.WriteLine($"[{DateTime.Now}] Form {form} of species {speciesIndex} not present in LGPE. Skipping.");
                            continue;
                        }

                        if (!learnSource7GG.TryGetPersonal(speciesIndex, form, out var personalInfo))
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

                        var evo = new EvoCriteria
                        {
                            Species = speciesIndex,
                            Form = form,
                            LevelMax = 100,
                        };

                        // Process level-up moves (including Move Reminder)
                        var learnset = learnSource7GG.GetLearnset(speciesIndex, form);
                        foreach (var moveId in learnset.GetMoveRange(100)) // 100 is the bonus for Move Reminder in LGPE
                        {
                            allMoves[moveId] = Math.Min(allMoves.ContainsKey(moveId) ? allMoves[moveId] : int.MaxValue, learnset.GetLevelLearnMove(moveId));
                        }

                        // Process TM moves and special tutor moves
                        for (ushort move = 0; move < gameStrings.movelist.Length; move++)
                        {
                            var learnInfo = learnSource7GG.GetCanLearn(new PK7(), personalInfo, evo, move);
                            if (learnInfo.Method is LearnMethod.TMHM or LearnMethod.Tutor)
                            {
                                allMoves[move] = Math.Min(allMoves.ContainsKey(move) ? allMoves[move] : int.MaxValue, 0);
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
            if (moveId > Legal.MaxMoveID_7b)
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Move ID {moveId} exceeds MaxMoveID_7b. Skipping.");
                return;
            }

            var moveName = gameStrings.movelist[moveId];
            if (string.IsNullOrEmpty(moveName))
            {
                errorLogger.WriteLine($"[{DateTime.Now}] Empty move name for ID {moveId}. Skipping.");
                return;
            }

            var moveType = gameStrings.types[MoveInfo.GetType(moveId, EntityContext.Gen7b)];
            var pp = MoveInfo.GetPP(EntityContext.Gen7b, moveId);
            var power = MoveInfo.GetPower(moveId, EntityContext.Gen7b);
            var accuracy = MoveInfo.GetAccuracy(moveId, EntityContext.Gen7b);
            var categoryByte = MoveInfo.GetCategory(moveId, EntityContext.Gen7b);
            var category = categoryByte switch
            {
                0 => "Status",
                1 => "Physical",
                2 => "Special",
                _ => "Unknown"
            };

            writer.WriteLine($"{fullPokemonName},{dexNumber},{moveName},{level},{moveType},{power},{accuracy},lgpe,{pp},{category}");
            errorLogger.WriteLine($"[{DateTime.Now}] Processed move: {moveName} for {fullPokemonName} at level {level}");
        }
    }
}
