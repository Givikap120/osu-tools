using System;
using System.Collections.Generic;
using System.Linq;
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

        private static float getARforRA(float desiredAR, float rate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(desiredAR, 1800, 1200, 450) * rate;
            return (float)IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, 1800, 1200, 450);
        }

        public static void TestAR(IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            float? savedAR = DA.ApproachRate.Value;

            for (float AR = 0; AR <= 11.01f;)
            {
                DA.ApproachRate.Value = AR;
                var (difficulty, performance) = calc(localMods);

                if (Math.Abs(AR - difficulty.ApproachRate) > 0.01)
                    Console.WriteLine($"AR{AR:0.##}->{difficulty.ApproachRate:0.##}: {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
                else
                    Console.WriteLine($"AR{AR:0.##}: {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");

                if (AR < 3.99f) AR += 0.1f; //1
                else if (AR < 3.99f) AR += 0.1f; //0.5
                else if (AR < 6.99f) AR += 0.1f;
                else if (AR < 9.99f) AR += 0.1f;
                else AR += 0.1f;
            }

            if (savedAR != null) DA.ApproachRate.Value = savedAR;
        }

        public static void TestDT(IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            for (float rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // NO MOD
            localMods = new List<Mod>(appliedMods);
            {
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }
        }

        public static void TestDTFixedAR(IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            var difficultyAttributes = calc(localMods).difficulty;
            float NMAR = (float)difficultyAttributes.ApproachRate;

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            float? savedAR = DA.ApproachRate.Value;
            for (float rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = getARforRA(NMAR, rate);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // NO MOD
            localMods = new List<Mod>(appliedMods);
            if (savedAR.IsNotNull()) DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            {
                if (savedAR.IsNotNull()) DA.ApproachRate.Value = savedAR;
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = getARforRA(NMAR, rate);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }

            if (savedAR != null) DA.ApproachRate.Value = savedAR;
        }

        public static void TestCS(IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = new List<Mod>(appliedMods);

            // HALF TIME
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (float CS = 0f; CS <= 7.01f; CS += 0.5f)
            {
                DA.CircleSize.Value = CS;
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"CS{CS:0.0#} (AR{difficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
            }
        }
    }
}
