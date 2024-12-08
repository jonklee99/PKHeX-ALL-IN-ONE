using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PKHeX.Core.LegalBallGenerator
{
    public static class BallLegalityGeneratorSV
    {
        private static readonly Dictionary<Ball, int> BallMap = new()
        {
            { Ball.Master, 1 },
            { Ball.Poke, 14 },
            { Ball.Great, 13 },
            { Ball.Ultra, 12 },
            { Ball.Safari, 15 },
            { Ball.Net, 16 },
            { Ball.Dive, 17 },
            { Ball.Nest, 18 },
            { Ball.Repeat, 19 },
            { Ball.Timer, 20 },
            { Ball.Luxury, 21 },
            { Ball.Premier, 22 },
            { Ball.Dusk, 23 },
            { Ball.Heal, 24 },
            { Ball.Quick, 25 },
            { Ball.Cherish, 26 },
            { Ball.Fast, 27 },
            { Ball.Level, 28 },
            { Ball.Lure, 29 },
            { Ball.Heavy, 30 },
            { Ball.Love, 31 },
            { Ball.Friend, 32 },
            { Ball.Moon, 33 },
            { Ball.Sport, 34 },
            { Ball.Dream, 4 },
            { Ball.Beast, 11 }
        };

        public static void GenerateBallLegalityCSV(string outputPath, string errorLogPath)
        {
            try
            {
                using var errorLogger = new StreamWriter(errorLogPath, false, Encoding.UTF8);
                using var csvWriter = new StreamWriter(outputPath, false, Encoding.UTF8);

                errorLogger.WriteLine($"[{DateTime.Now}] Starting CSV generation for ball legality in SV");
                csvWriter.WriteLine("Name,Balls");

                var pt = PersonalTable.SV;
                var gameStrings = GameInfo.GetStrings("en");

                for (ushort species = 1; species <= pt.MaxSpeciesID; species++)
                {
                    var pi = pt.GetFormEntry(species, 0);
                    if (pi == null || !pi.IsPresentInGame)
                        continue;

                    for (byte form = 0; form < pi.FormCount; form++)
                    {
                        var formInfo = pt.GetFormEntry(species, form);
                        if (formInfo == null || !formInfo.IsPresentInGame)
                            continue;

                        string name = gameStrings.specieslist[species];
                        if (form > 0)
                            name += $"-{form}";

                        var legalBalls = GetLegalBallsSV(species, form);
                        var ballString = string.Join(",", legalBalls);

                        csvWriter.WriteLine($"{name},{ballString}");
                        errorLogger.WriteLine($"[{DateTime.Now}] Processed {name}");
                    }
                }

                errorLogger.WriteLine($"[{DateTime.Now}] CSV file generated successfully at: {outputPath}");
            }
            catch (Exception ex)
            {
                using var errorLogger = new StreamWriter(errorLogPath, true, Encoding.UTF8);
                errorLogger.WriteLine($"[{DateTime.Now}] An error occurred: {ex.Message}");
                errorLogger.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private static List<string> GetLegalBallsSV(ushort species, byte form)
        {
            var legalBalls = new List<string>();
            var ballPermit = species is >= (int)Species.Sprigatito and <= (int)Species.Quaquaval
                ? BallUseLegality.WildPokeballs8g_WithoutRaid
                : BallUseLegality.WildPokeballs9;

            foreach (var (ball, id) in BallMap)
            {
                if (BallUseLegality.IsBallPermitted(ballPermit, (byte)ball))
                {
                    legalBalls.Add($"{id}(1)"); // Using 1 as default level like the scraper
                }
            }

            return legalBalls;
        }
    }
}
