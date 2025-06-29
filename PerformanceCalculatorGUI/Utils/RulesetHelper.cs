// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace PerformanceCalculatorGUI
{
    public static class RulesetHelper
    {
        public static DifficultyCalculator GetExtendedDifficultyCalculator(RulesetInfo ruleset, IWorkingBeatmap working)
        {
            return ruleset.OnlineID switch
            {
                0 => new ExtendedOsuDifficultyCalculator(ruleset, working),
                1 => new ExtendedTaikoDifficultyCalculator(ruleset, working),
                2 => new ExtendedCatchDifficultyCalculator(ruleset, working),
                3 => new ExtendedManiaDifficultyCalculator(ruleset, working),
                _ => ruleset.CreateInstance().CreateDifficultyCalculator(working)
            };
        }

        public static Ruleset GetRulesetFromLegacyID(int id)
        {
            return id switch
            {
                0 => new OsuRuleset(),
                1 => new TaikoRuleset(),
                2 => new CatchRuleset(),
                3 => new ManiaRuleset(),
                _ => throw new ArgumentException("Invalid ruleset ID provided.")
            };
        }

        /// <summary>
        /// Generates the unique hash of mods combo that affect difficulty calculation
        /// Needs to be updated if list of difficulty adjusting mods changes
        /// </summary>
        public static int GenerateModsHash(Mod[] mods, BeatmapDifficulty difficulty, RulesetInfo ruleset)
        {
            // Rate changing mods
            double rate = ModUtils.CalculateRateWithMods(mods);

            int hash = 0;

            if (ruleset.OnlineID == 0) // For osu we have many different things
            {
                BeatmapDifficulty d = new BeatmapDifficulty(difficulty);

                foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                    mod.ApplyToDifficulty(d);

                bool isSliderAccuracy = mods.OfType<OsuModClassic>().All(m => !m.NoSliderHeadAccuracy.Value);

                byte flashlightHash = 0;
                if (mods.Any(h => h is OsuModFlashlight))
                {
                    if (!mods.Any(h => h is OsuModHidden)) flashlightHash = 1;
                    else flashlightHash = 2;
                }

                byte mirrorHash = 0;

                if (mods.Any(m => m is OsuModHardRock))
                {
                    mirrorHash = 1 + (int)OsuModMirror.MirrorType.Vertical;
                }
                else if (mods.FirstOrDefault(m => m is OsuModMirror) is OsuModMirror mirror)
                {
                    mirrorHash = (byte)(1 + (int)mirror.Reflection.Value);
                }

                hash = HashCode.Combine(rate, d.CircleSize, d.OverallDifficulty, d.ApproachRate, isSliderAccuracy, flashlightHash, mirrorHash);
            }
            else if (ruleset.OnlineID == 1) // For taiko we only have rate
            {
                hash = rate.GetHashCode();
            }
            else if (ruleset.OnlineID == 2) // For catch we have rate and CS
            {
                BeatmapDifficulty d = new BeatmapDifficulty(difficulty);

                foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                    mod.ApplyToDifficulty(d);

                hash = HashCode.Combine(rate, d.CircleSize);
            }
            else if (ruleset.OnlineID == 3) // Mania is using rate, and keys data for converts
            {
                int keyCount = 0;

                if (mods.FirstOrDefault(h => h is ManiaKeyMod) is ManiaKeyMod mod)
                    keyCount = mod.KeyCount;

                bool isDualStages = mods.Any(h => h is ManiaModDualStages);

                hash = HashCode.Combine(rate, keyCount, isDualStages);
            }

            return hash;
        }

        public static int AdjustManiaScore(int score, IReadOnlyList<Mod> mods)
        {
            if (score != 1000000) return score;

            double scoreMultiplier = 1;

            // Cap score depending on difficulty adjustment mods (matters for mania).
            foreach (var mod in mods)
            {
                if (mod.Type == ModType.DifficultyReduction)
                    scoreMultiplier *= mod.ScoreMultiplier;
            }

            return (int)Math.Round(1000000 * scoreMultiplier);
        }

        public static Dictionary<HitResult, int> GenerateHitResultsForRuleset(RulesetInfo ruleset, double accuracy, IBeatmap beatmap, Mod[] mods, int countMiss, int? countMeh, int? countGood, int? countLargeTickMisses, int? countSliderTailMisses)
        {
            return ruleset.OnlineID switch
            {
                0 => GenerateOsuHitResults(accuracy, beatmap, countMiss, countMeh, countGood, countLargeTickMisses, countSliderTailMisses),
                1 => generateTaikoHitResults(accuracy, beatmap, countMiss, countGood),
                2 => generateCatchHitResults(accuracy, beatmap, countMiss, countMeh, countGood),
                3 => generateManiaHitResults(accuracy, beatmap, mods, countMiss),
                _ => throw new ArgumentException("Invalid ruleset ID provided.")
            };
        }

        public static Dictionary<HitResult, int> GenerateOsuHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countMeh, int? countGood, int? countLargeTickMisses, int? countSliderTailMisses)
        {
            int countGreat;

            int totalResultCount = beatmap.HitObjects.Count;

            if (countMeh != null || countGood != null)
            {
                countGreat = Math.Max(totalResultCount - (countGood ?? 0) - (countMeh ?? 0) - countMiss, 0);
            }
            else
            {
                // Total result count excluding countMiss
                int relevantResultCount = totalResultCount - countMiss;

                // Accuracy excluding countMiss. We need that because we're trying to achieve target accuracy without touching countMiss
                // So it's better to pretened that there were 0 misses in the 1st place
                double relevantAccuracy = accuracy * totalResultCount / relevantResultCount;

                // Clamp accuracy to account for user trying to break the algorithm by inputting impossible values
                relevantAccuracy = Math.Clamp(relevantAccuracy, 0, 1);

                // Main curve for accuracy > 25%, the closer accuracy is to 25% - the more 50s it adds
                if (relevantAccuracy >= 0.25)
                {
                    // Main curve. Zero 50s if accuracy is 100%, one 50 per 9 100s if accuracy is 75% (excluding misses), 4 50s per 9 100s if accuracy is 50%
                    double ratio50To100 = Math.Pow(1 - (relevantAccuracy - 0.25) / 0.75, 2);

                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c50 = c100 * ratio50to100
                    double count100Estimate = 6 * relevantResultCount * (1 - relevantAccuracy) / (5 * ratio50To100 + 4);

                    // Get count50 according to c50 = c100 * ratio50to100
                    double count50Estimate = count100Estimate * ratio50To100;

                    // Round it to get int number of 100s
                    countGood = (int?)Math.Round(count100Estimate);

                    // Get number of 50s as difference between total mistimed hits and count100
                    countMeh = (int?)(Math.Round(count100Estimate + count50Estimate) - countGood);
                }
                // If accuracy is between 16.67% and 25% - we assume that we have no 300s
                else if (relevantAccuracy >= 1.0 / 6)
                {
                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c300 = 0
                    double count100Estimate = 6 * relevantResultCount * relevantAccuracy - relevantResultCount;

                    // We only had 100s and 50s in that scenario so rest of the hits are 50s
                    double count50Estimate = relevantResultCount - count100Estimate;

                    // Round it to get int number of 100s
                    countGood = (int?)Math.Round(count100Estimate);

                    // Get number of 50s as difference between total mistimed hits and count100
                    countMeh = (int?)(Math.Round(count100Estimate + count50Estimate) - countGood);
                }
                // If accuracy is less than 16.67% - it means that we have only 50s or misses
                // Assuming that we removed misses in the 1st place - that means that we need to add additional misses to achieve target accuracy
                else
                {
                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c300 = c100 = 0
                    double count50Estimate = 6 * relevantResultCount * relevantAccuracy;

                    // We have 0 100s, because we can't start adding 100s again after reaching "only 50s" point
                    countGood = 0;

                    // Round it to get int number of 50s
                    countMeh = (int?)Math.Round(count50Estimate);

                    // Fill the rest results with misses overwriting initial countMiss
                    countMiss = (int)(totalResultCount - countMeh);
                }

                // Rest of the hits are 300s
                countGreat = (int)(totalResultCount - countGood - countMeh - countMiss);
            }

            var result = new Dictionary<HitResult, int>
            {
                { HitResult.Great, countGreat },
                { HitResult.Ok, countGood ?? 0 },
                { HitResult.Meh, countMeh ?? 0 },
                { HitResult.Miss, countMiss }
            };

            if (countLargeTickMisses != null)
                result[HitResult.LargeTickMiss] = countLargeTickMisses.Value;

            if (countSliderTailMisses != null)
                result[HitResult.SliderTailHit] = beatmap.HitObjects.Count(x => x is Slider) - countSliderTailMisses.Value;

            return result;
        }

        private static Dictionary<HitResult, int> generateTaikoHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countGood)
        {
            int totalResultCount = beatmap.HitObjects.OfType<Hit>().Count();

            int countGreat;

            if (countGood != null)
            {
                countGreat = (int)(totalResultCount - countGood - countMiss);
            }
            else
            {
                // Let Great=2, Good=1, Miss=0. The total should be this.
                int targetTotal = (int)Math.Round(accuracy * totalResultCount * 2);

                countGreat = targetTotal - (totalResultCount - countMiss);
                countGood = totalResultCount - countGreat - countMiss;
            }

            return new Dictionary<HitResult, int>
            {
                { HitResult.Great, countGreat },
                { HitResult.Ok, (int)countGood },
                { HitResult.Meh, 0 },
                { HitResult.Miss, countMiss }
            };
        }

        private static Dictionary<HitResult, int> generateCatchHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countMeh, int? countGood)
        {
            int maxCombo = beatmap.HitObjects.Count(h => h is Fruit) + beatmap.HitObjects.OfType<JuiceStream>().SelectMany(j => j.NestedHitObjects).Count(h => !(h is TinyDroplet));

            int maxTinyDroplets = beatmap.HitObjects.OfType<JuiceStream>().Sum(s => s.NestedHitObjects.OfType<TinyDroplet>().Count());
            int maxDroplets = beatmap.HitObjects.OfType<JuiceStream>().Sum(s => s.NestedHitObjects.OfType<Droplet>().Count()) - maxTinyDroplets;
            int maxFruits = beatmap.HitObjects.OfType<Fruit>().Count() + 2 * beatmap.HitObjects.OfType<JuiceStream>().Count() + beatmap.HitObjects.OfType<JuiceStream>().Sum(s => s.RepeatCount);

            // Either given or max value minus misses
            int countDroplets = countGood ?? Math.Max(0, maxDroplets - countMiss);

            // Max value minus whatever misses are left. Negative if impossible missCount
            int countFruits = maxFruits - (countMiss - (maxDroplets - countDroplets));

            // Either given or the max amount of hit objects with respect to accuracy minus the already calculated fruits and drops.
            // Negative if accuracy not feasable with missCount.
            int countTinyDroplets = countMeh ?? (int)Math.Round(accuracy * (maxCombo + maxTinyDroplets)) - countFruits - countDroplets;

            // Whatever droplets are left
            int countTinyMisses = maxTinyDroplets - countTinyDroplets;

            return new Dictionary<HitResult, int>
            {
                { HitResult.Great, countFruits },
                { HitResult.LargeTickHit, countDroplets },
                { HitResult.SmallTickHit, countTinyDroplets },
                { HitResult.SmallTickMiss, countTinyMisses },
                { HitResult.Miss, countMiss }
            };
        }

        private static Dictionary<HitResult, int> generateManiaHitResults(double accuracy, IBeatmap beatmap, Mod[] mods, int countMiss)
        {
            int totalHits = beatmap.HitObjects.Count;
            if (!mods.Any(m => m is ModClassic))
                totalHits += beatmap.HitObjects.Count(ho => ho is HoldNote);

            int perfectValue = mods.Any(m => m is ModClassic) ? 60 : 61;

            // Let Great = 60, Good = 40, Ok = 20, Meh = 10, Miss = 0, Perfect = 61 or 60 depending on CL. The total should be this.
            int targetTotal = (int)Math.Round(accuracy * totalHits * perfectValue);

            // Start by assuming every non miss is a meh
            // This is how much increase is needed by the rest
            int remainingHits = totalHits - countMiss;
            int delta = Math.Max(targetTotal - (10 * remainingHits), 0);

            // Each perfect increases total by 50 (CL) or 51 (no CL) (perfect - meh = 50 or 51)
            int perfects = Math.Min(delta / (perfectValue - 10), remainingHits);
            delta -= perfects * (perfectValue - 10);
            remainingHits -= perfects;

            // Each great increases total by 50 (great - meh = 50)
            int greats = Math.Min(delta / 50, remainingHits);
            delta -= greats * 50;
            remainingHits -= greats;

            // Each good increases total by 30 (good - meh = 30)
            int goods = Math.Min(delta / 30, remainingHits);
            delta -= goods * 30;
            remainingHits -= goods;

            // Each ok increases total by 10 (ok - meh = 10)
            int oks = Math.Min(delta / 10, remainingHits);
            remainingHits -= oks;

            // Everything else is a meh, as initially assumed
            int mehs = remainingHits;

            return new Dictionary<HitResult, int>
            {
                { HitResult.Perfect, perfects },
                { HitResult.Great, greats },
                { HitResult.Ok, oks },
                { HitResult.Good, goods },
                { HitResult.Meh, mehs },
                { HitResult.Miss, countMiss }
            };
        }

        public static double GetAccuracyForRuleset(RulesetInfo ruleset, IBeatmap beatmap, Dictionary<HitResult, int> statistics, Mod[] mods)
        {
            return ruleset.OnlineID switch
            {
                0 => getOsuAccuracy(beatmap, statistics),
                1 => getTaikoAccuracy(statistics),
                2 => getCatchAccuracy(statistics),
                3 => getManiaAccuracy(statistics, mods),
                _ => 0.0
            };
        }

        public static List<T> FilterDuplicateScores<T>(List<T> scores, Func<T, ScoreInfo> scoreInfoSelector)
        {
            List<T> newScores = [];

            ScoreInfo previousScoreInfo = null;

            static bool isDuplicate(ScoreInfo s1, ScoreInfo s2) => s1.Date == s2.Date && s1.TotalScore == s2.TotalScore && s1.User.Username == s2.User.Username && s1.ModsJson == s2.ModsJson;

            foreach (var score in scores)
            {
                var scoreInfo = scoreInfoSelector(score);

                if (previousScoreInfo != null && isDuplicate(scoreInfo, previousScoreInfo))
                {
                    // If previous is flawed while this is good - delete previous
                    if ((scoreInfo.LegacyOnlineID > 0 && previousScoreInfo.LegacyOnlineID <= 0) || (scoreInfo.OnlineID > 0 && previousScoreInfo.OnlineID <= 0))
                    {
                        newScores.RemoveAt(newScores.Count - 1);
                        newScores.Add(score);

                        previousScoreInfo = scoreInfo;
                    }
                }
                else
                {
                    newScores.Add(score);
                    previousScoreInfo = scoreInfo;
                }
            }

            return newScores;
        }

        public static List<ScoreInfo> FilterDuplicateScores(List<ScoreInfo> scores) => FilterDuplicateScores(scores, s => s);

        private static string getValidRealmCopyName(string lazerPath)
        {
            string destinationPath = Path.Combine(lazerPath, @"client_osutools_copy.realm");

            int copyNumber = 1;

            static bool isFileLocked(string path)
            {
                try
                {
                    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        stream.Close();
                    }
                }
                catch (IOException ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }

            // Loop until an available name is found
            while (isFileLocked(destinationPath))
            {
                // Generate a new file name with an incrementing number
                destinationPath = Path.Combine(
                    lazerPath,
                    $"client_osutools_copy({copyNumber++}).realm"
                );
            }

            return destinationPath;
        }

        public static RealmAccess GetRealmAccess(GameHost gameHost, string lazerPath)
        {
            var storage = gameHost.GetStorage(lazerPath);
            File.Copy(Path.Combine(lazerPath, @"client.realm"), getValidRealmCopyName(lazerPath), true);
            var realmAccess = new RealmAccess(storage, @"client_osutools_copy.realm");
            return realmAccess;
        }

        private static double getOsuAccuracy(IBeatmap beatmap, Dictionary<HitResult, int> statistics)
        {
            int countGreat = statistics[HitResult.Great];
            int countGood = statistics[HitResult.Ok];
            int countMeh = statistics[HitResult.Meh];
            int countMiss = statistics[HitResult.Miss];

            double total = 6 * countGreat + 2 * countGood + countMeh;
            double max = 6 * (countGreat + countGood + countMeh + countMiss);

            if (statistics.TryGetValue(HitResult.SliderTailHit, out int countSliderTailHit))
            {
                int countSliders = beatmap.HitObjects.Count(x => x is Slider);

                total += 3 * countSliderTailHit;
                max += 3 * countSliders;
            }

            if (statistics.TryGetValue(HitResult.LargeTickMiss, out int countLargeTicksMiss))
            {
                int countLargeTicks = beatmap.HitObjects.Sum(obj => obj.NestedHitObjects.Count(x => x is SliderTick or SliderRepeat));
                int countLargeTickHit = countLargeTicks - countLargeTicksMiss;

                total += 0.6 * countLargeTickHit;
                max += 0.6 * countLargeTicks;
            }

            return total / Math.Max(max, 1);
        }

        private static double getTaikoAccuracy(Dictionary<HitResult, int> statistics)
        {
            int countGreat = statistics[HitResult.Great];
            int countGood = statistics[HitResult.Ok];
            int countMiss = statistics[HitResult.Miss];
            int total = countGreat + countGood + countMiss;

            return (double)((2 * countGreat) + countGood) / (2 * total);
        }

        private static double getCatchAccuracy(Dictionary<HitResult, int> statistics)
        {
            double hits = statistics[HitResult.Great] + statistics[HitResult.LargeTickHit] + statistics[HitResult.SmallTickHit];
            double total = hits + statistics[HitResult.Miss] + statistics[HitResult.SmallTickMiss];

            return hits / total;
        }

        private static double getManiaAccuracy(Dictionary<HitResult, int> statistics, Mod[] mods)
        {
            int countPerfect = statistics[HitResult.Perfect];
            int countGreat = statistics[HitResult.Great];
            int countGood = statistics[HitResult.Good];
            int countOk = statistics[HitResult.Ok];
            int countMeh = statistics[HitResult.Meh];
            int countMiss = statistics[HitResult.Miss];

            int perfectWeight = mods.Any(m => m is ModClassic) ? 300 : 305;

            double total = (perfectWeight * countPerfect) + (300 * countGreat) + (200 * countGood) + (100 * countOk) + (50 * countMeh);
            double max = perfectWeight * (countPerfect + countGreat + countGood + countOk + countMeh + countMiss);

            return total / max;
        }
    }
}
