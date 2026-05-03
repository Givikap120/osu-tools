using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

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

        private static List<Mod> cloneMods(IEnumerable<Mod> mods)
        {
            List<Mod> clonedMods = [];

            foreach (var mod in mods)
            {
                clonedMods.Add(mod.DeepClone());
            }

            return clonedMods;
        }

        // WARNING: copied from new version of pp so you don't have to depend on it
        public static double CalculateRateAdjustedApproachRate(double approachRate, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        public static double CalculateRateAdjustedOverallDifficulty(double overallDifficulty, double clockRate)
        {
            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(overallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;

            return (79.5 - hitWindowGreat) / 6;
        }

        private static string getReading(OsuPerformanceAttributes performance)
        {
            return "";
            //return $"- {performance.Reading:0} reading pp";
        }

        private static double getARPostDT(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods)
        {
            var adjustedDifficulty = beatmapDifficulty.Clone();
            appliedMods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(adjustedDifficulty));
            double rate = ModUtils.CalculateRateWithMods(appliedMods);
            return CalculateRateAdjustedApproachRate(adjustedDifficulty.ApproachRate, rate);
        }

        public static void TestAR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = cloneMods(appliedMods);

            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);

            for (float baseAR = 0; baseAR <= 11.01f;)
            {
                DA.ApproachRate.Value = baseAR;
                double realAR = getARPostDT(beatmapDifficulty, localMods);

                var (difficulty, performance) = calc(localMods);

                if (Math.Abs(baseAR - realAR) > 0.01)
                    Console.WriteLine($"AR{baseAR:0.##}\t->\t{realAR:0.##}:\t{difficulty.StarRating:0.##}*\t{performance.Total:0}pp\t{getReading(performance)}");
                else
                    Console.WriteLine($"AR{baseAR:0.##}:\t{difficulty.StarRating:0.##}*\t{performance.Total:0}pp\t{getReading(performance)}");

                if (baseAR < 3.99f) baseAR += 0.1f; //1
                else if (baseAR < 3.99f) baseAR += 0.1f; //0.5
                else if (baseAR < 6.99f) baseAR += 0.1f;
                else if (baseAR < 9.99f) baseAR += 0.1f;
                else baseAR += 0.1f;
            }
        }

        public static void TestDT(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = cloneMods(appliedMods);

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            for (float rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }

            // NO MOD
            localMods = cloneMods(appliedMods);
            {
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }
        }

        public static void TestDTFixedAR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = cloneMods(appliedMods);

            double desiredAR = getARPostDT(beatmapDifficulty, appliedMods);

            // HALF TIME
            OsuModHalfTime HT = getModOrAdd<OsuModHalfTime>(localMods);
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (double rate = 0.5f; rate <= 0.99f; rate += 0.05f)
            {
                HT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = (float?)CalculateRateAdjustedApproachRate(desiredAR, 1.0 / rate);
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }

            // NO MOD
            localMods = cloneMods(appliedMods);
            {
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"1.0x (AR{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }

            // DOUBLE TIME
            OsuModDoubleTime DT = getModOrAdd<OsuModDoubleTime>(localMods);
            DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            for (float rate = 1.05f; rate <= 2.01f; rate += 0.05f)
            {
                DT.SpeedChange.Value = rate;
                DA.ApproachRate.Value = (float?)CalculateRateAdjustedApproachRate(desiredAR, 1.0 / rate);
                double realAR = getARPostDT(beatmapDifficulty, appliedMods);
                var (difficulty, performance) = calc(localMods);
                Console.WriteLine($"{rate:0.0#}x (AR{DA.ApproachRate.Value:0.##}->{realAR:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp {getReading(performance)}");
            }
        }

        public static void TestCS(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = cloneMods(appliedMods);
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);

            for (float CS = 2f; CS <= 8.01f; CS += 0.1f)
            {
                DA.CircleSize.Value = CS;
                var (difficulty, performance) = calc(localMods);
                //Console.WriteLine($"CS{CS:0.0#} (AR{beatmapDifficulty.ApproachRate:0.##}): {difficulty.StarRating:0.##}* {performance.Total:0}pp ({getCognition(performance):0} cognition pp)");
                Console.WriteLine($"CS{CS:0.0#}: {difficulty.StarRating:0.##}* {performance.Total:0}pp ({performance.Aim:0}; aim pp {performance.Speed:0} speed pp; {getReading(performance)})");
                //Console.WriteLine($"{CS:0.0#},{difficulty.StarRating:0.##},{performance.Total:0},{performance.Aim:0},{performance.Speed:0}");
            }
        }

        public static void TestCS_alt(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            List<Mod> localMods = cloneMods(appliedMods);
            OsuModDifficultyAdjust DA = getModOrAdd<OsuModDifficultyAdjust>(localMods);

            const double base_cs = 0;
            double baseRadius = 54.4 - 4.48 * base_cs;

            //const double base_spacing = 60.5;

            for (double d = 1; d <= 4; d += 0.01)
            {
                double radius = baseRadius / d;
                double CS = (54.4 - radius) / 4.48;

                DA.CircleSize.Value = (float)CS;
                var (difficulty, performance) = calc(localMods);

                //double spacing = d * base_spacing / (2 * OsuDifficultyHitObject.NORMALISED_RADIUS);

                Console.WriteLine($"{d:0.0#},{difficulty.StarRating:0.##},{performance.Total:0},{performance.Aim:0},{performance.Speed:0}");
            }
        }

        private static double[] normalizeWeighted((double value, double delta)[] input, double targetSum)
        {
            int n = input.Length;
            double[] result = new double[n];

            double currentSum = 0.0;
            double weightedSum = 0.0;

            for (int i = 0; i < n; i++)
            {
                currentSum += input[i].value;
                weightedSum += input[i].value * input[i].delta;
            }

            if (weightedSum == 0.0) currentSum = weightedSum;

            double k = (targetSum - currentSum) / weightedSum;

            for (int i = 0; i < n; i++)
                result[i] = input[i].value * (1.0 + k * input[i].delta);

            return result;
        }


        public static void TestHR(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            var localMods = cloneMods(appliedMods);
            var da = getModOrAdd<OsuModDifficultyAdjust>(localMods);

            var beatmapDifficultyHR = beatmapDifficulty.Clone();
            new OsuModHardRock().ApplyToDifficulty(beatmapDifficultyHR);

            applyDifficultyToDA(beatmapDifficulty, da);
            double baseVal = calc(localMods).performance.Total;

            applyDifficultyToDA(beatmapDifficultyHR, da);
            double hrVal = calc(localMods).performance.Total;

            Console.WriteLine($"{baseVal:0}pp -> {hrVal:0}pp ({hrVal - baseVal:+0;-0;0}pp)");

            var csResult = testParam("CS", beatmapDifficulty, beatmapDifficultyHR, da, calc, localMods, baseVal, hrVal);
            var arResult = testParam("AR", beatmapDifficulty, beatmapDifficultyHR, da, calc, localMods, baseVal, hrVal);
            var odResult = testParam("OD", beatmapDifficulty, beatmapDifficultyHR, da, calc, localMods, baseVal, hrVal);

            double[] results = normalizeWeighted([csResult, arResult, odResult], hrVal - baseVal);

            printNormed("CS", results[0]);
            printNormed("AR", results[1]);
            printNormed("OD", results[2]);
        }

        public static void TestEZ(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            var localMods = cloneMods(appliedMods);
            var da = getModOrAdd<OsuModDifficultyAdjust>(localMods);

            var beatmapDifficultyEZ = beatmapDifficulty.Clone();
            new OsuModEasy().ApplyToDifficulty(beatmapDifficultyEZ);

            applyDifficultyToDA(beatmapDifficulty, da);
            double baseVal = calc(localMods).performance.Total;

            applyDifficultyToDA(beatmapDifficultyEZ, da);
            double ezVal = calc(localMods).performance.Total;

            Console.WriteLine($"{baseVal:0}pp -> {ezVal:0}pp ({ezVal - baseVal:+0;-0;0}pp)");

            var csResult = testParam("CS", beatmapDifficulty, beatmapDifficultyEZ, da, calc, localMods, baseVal, ezVal);
            var arResult = testParam("AR", beatmapDifficulty, beatmapDifficultyEZ, da, calc, localMods, baseVal, ezVal);
            var odResult = testParam("OD", beatmapDifficulty, beatmapDifficultyEZ, da, calc, localMods, baseVal, ezVal);

            double[] results = normalizeWeighted([csResult, arResult, odResult], ezVal - baseVal);

            printNormed("CS", results[0]);
            printNormed("AR", results[1]);
            printNormed("OD", results[2]);
        }

        public static void TestAROD10(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            var localMods = cloneMods(appliedMods);
            beatmapDifficulty = beatmapDifficulty.Clone();

            var da = getModOrAdd<OsuModDifficultyAdjust>(localMods);
            localMods.RemoveAll(m => m is OsuModHidden);

            double clockRate = ModUtils.CalculateRateWithMods(localMods);
            localMods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(beatmapDifficulty));

            float adjustedAR = (float)CalculateRateAdjustedApproachRate(10, 1.0 / clockRate);
            float adjustedOD = (float)CalculateRateAdjustedOverallDifficulty(10, 1.0 / clockRate);

            applyDifficultyToDA(beatmapDifficulty, da);
            double baseVal = calc(localMods).performance.Total;

            var beatmapDifficultyAR = beatmapDifficulty.Clone();
            beatmapDifficultyAR.ApproachRate = adjustedAR;
            applyDifficultyToDA(beatmapDifficultyAR, da);
            double arVal = calc(localMods).performance.Total;

            var beatmapDifficultyOD = beatmapDifficulty.Clone();
            beatmapDifficultyOD.OverallDifficulty = adjustedOD;
            applyDifficultyToDA(beatmapDifficultyOD, da);
            double odVal = calc(localMods).performance.Total;

            var beatmapDifficultyAROD = beatmapDifficulty.Clone();
            beatmapDifficultyAROD.ApproachRate = adjustedAR;
            beatmapDifficultyAROD.OverallDifficulty = adjustedOD;
            applyDifficultyToDA(beatmapDifficultyAROD, da);
            double arodVal = calc(localMods).performance.Total;

            localMods.Add(new OsuModHidden());
            applyDifficultyToDA(beatmapDifficulty, da);
            double baseValHD = calc(localMods).performance.Total;

            Console.WriteLine($"{arodVal:0}pp -> {baseVal:0}pp ({baseVal - arodVal:+0;-0;0}pp) -> {baseValHD:0}pp ({baseValHD - arodVal:+0;-0;0}pp)");

            double hdResultValue = baseValHD - baseVal;
            double arhdResultValue = testParam("AR", beatmapDifficultyAR, beatmapDifficulty, da, calc, localMods, arVal, baseValHD, () => localMods.RemoveAll(m => m is OsuModHidden)).value;
            var arResult = testParam("AR", beatmapDifficultyAR, beatmapDifficulty, da, calc, localMods, arVal, baseVal);
            var odResult = testParam("OD", beatmapDifficultyOD, beatmapDifficulty, da, calc, localMods, odVal, baseVal);

            double arhdMultipier = arhdResultValue / (arResult.value + hdResultValue);
            arResult.value *= arhdMultipier;
            hdResultValue *= arhdMultipier;

            double targetSum = baseVal - arodVal;
            double currentSum = arResult.value + odResult.value;
            double weightedSum = arResult.value * arResult.delta + odResult.value * arResult.delta;
            weightedSum = weightedSum == 0 ? currentSum : weightedSum;

            double k = (targetSum - currentSum) / weightedSum;

            double arMultiplier = 1.0 + k * arResult.delta;
            double odMultiplier = 1.0 + k * odResult.delta;

            printNormed("ARHD", arhdResultValue * arMultiplier);
            printNormed("AR", arResult.value * arMultiplier);
            printNormed("HD", hdResultValue * arMultiplier);
            printNormed("OD", odResult.value * odMultiplier);
        }

        public static void TestAcc(BeatmapDifficulty beatmapDifficulty, IReadOnlyList<Mod> appliedMods, Func<IReadOnlyList<Mod>, double, (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance)> calc)
        {
            for (double acc = 0.80; acc <= 0.981; acc += 0.01)
            {
                var result = calc(appliedMods, acc).performance;
                // Console.WriteLine($"{acc * 100:0}% - {result.SpeedDeviation * 10:0} UR: {result.Total:0}pp");
            }
        }

        private static (double value, double delta) testParam(string name, BeatmapDifficulty diff, BeatmapDifficulty diffAdj, OsuModDifficultyAdjust da, Func<IReadOnlyList<Mod>, (OsuDifficultyAttributes diffAttr, OsuPerformanceAttributes perfAttr)> calc, List<Mod> localMods, double baseVal, double adjVal, Action? stageAdjust = null)
        {
            double plus, minus;

            applyDifficultyToDA(diff, da);
            setParamInDA(da, diffAdj, name);
            plus = calc(localMods).perfAttr.Total;

            stageAdjust?.Invoke();

            applyDifficultyToDA(diffAdj, da);
            setParamInDA(da, diff, name);
            minus = calc(localMods).perfAttr.Total;

            double plusDelta = plus - baseVal;
            double minusDelta = adjVal - minus;

            //int result = (int)Math.Round(((plus - baseVal) + (adjVal - minus)) / 2);
            double result = Math.Abs(plusDelta) >= Math.Abs(minusDelta) ? minusDelta : plusDelta;
            double delta = Math.Abs(plusDelta - minusDelta);

            return (result, delta);
        }

        private static void printNormed(string name, double value)
        {
            Console.WriteLine($"{name}: {value:+0;-0;0}pp");
        }

        private static void setParamInDA(OsuModDifficultyAdjust DA, BeatmapDifficulty difficulty, string name)
        {
            switch (name)
            {
                case "CS": DA.CircleSize.Value = (float)difficulty.CircleSize; break;
                case "AR": DA.ApproachRate.Value = (float)difficulty.ApproachRate; break;
                case "OD": DA.OverallDifficulty.Value = (float)difficulty.OverallDifficulty; break;
            }
        }

        private static void applyDifficultyToDA(BeatmapDifficulty source, OsuModDifficultyAdjust DA)
        {
            DA.CircleSize.Value = source.CircleSize;
            DA.ApproachRate.Value = source.ApproachRate;
            DA.OverallDifficulty.Value = source.OverallDifficulty;
            DA.DrainRate.Value = source.DrainRate;
        }
    }
}
