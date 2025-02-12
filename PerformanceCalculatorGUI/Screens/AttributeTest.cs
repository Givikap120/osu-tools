using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;

namespace PerformanceCalculatorGUI.Screens
{
    public static class AttributeTest
    {
        private static T getModOrAdd<T>(IList<Mod> mods) where T : Mod, new()
        {
            T desiredMod;
            if (mods.Any(m => m is T))
            {
                desiredMod = (T)mods.First(m => m is T);
            }
            else
            {
                desiredMod = new T();
                mods.Add(desiredMod);
            }
            return desiredMod;
        }

        private static double getCognition(OsuPerformanceAttributes performance)
        {
            //return performance.Cognition;
            return double.NaN;
        }

        private static float getBaseAR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods)
        {
            var adjustedDifficulty = beatmapDifficulty.Clone();
            appliedMods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(adjustedDifficulty));
            return adjustedDifficulty.ApproachRate;
        }

        private static double getRate(IReadOnlyList<Mod> mods)
        {
            var track = new TrackVirtual(10000);
            mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            return track.Rate;
        }

        private static double getARPostDTInverse(double desiredAR, double rate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(desiredAR, 1800, 1200, 450) * rate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, 1800, 1200, 450);
        }

        private static double getARPostDT(double baseAR, double rate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(baseAR, 1800, 1200, 450) / rate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, 1800, 1200, 450);
        }

        private static double getARPostDT(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods)
        {
            float baseAR = getBaseAR(beatmapDifficulty, appliedMods);
            double rate = getRate(appliedMods);
            return getARPostDT(baseAR, rate);
        }

        public static void TestAR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            float? savedAR = DA.ApproachRate.Value;

            for (float baseAR = 0; baseAR <= 11.01f;)
            {
                DA.ApproachRate.Value = baseAR;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);

                var (difficulty, performance) = calc(localMods);

                if (Math.Abs(baseAR - realAR) > 0.01)
                    Console.WriteLine($"AR{baseAR:0.##}->{realAR:0.##}: {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
                else
                    Console.WriteLine($"AR{baseAR:0.##}: {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");

                if (baseAR < 3.99f) baseAR += 0.1f; //1
                else if (baseAR < 3.99f) baseAR += 0.1f; //0.5
                else if (baseAR < 6.99f) baseAR += 0.1f;
                else if (baseAR < 9.99f) baseAR += 0.1f;
                else baseAR += 0.1f;
            }

            if (savedAR != null) DA.ApproachRate.Value = savedAR;
        }

        public static void TestDT(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            for (float rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // NO MOD
            localMods = new List<Mod>(appliedMods);
            {
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }
        }

        public static void TestDTFixedAR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            double desiredAR = getARPostDT(beatmapDifficulty, appliedMods);

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            float? savedAR = DA.ApproachRate.Value;
            for (double rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = (float?)getARPostDTInverse(desiredAR, rate);
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // NO MOD
            localMods = new List<Mod>(appliedMods);
            if (savedAR.IsNotNull()) DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            {
                if (savedAR.IsNotNull()) DA.ApproachRate.Value = savedAR;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = (float?)getARPostDTInverse(desiredAR, rate);
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            if (savedAR != null) DA.ApproachRate.Value = savedAR;
        }

        public static void TestCS(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            // HALF TIME
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (float CS = 0f; CS <= 7.01f; CS += 0.5f)
            {
                DA.CircleSize.Value = CS;
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"CS{CS:0.0#} (AR{beatmapDifficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }
        }
    }
}
