// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    public static class LazerScoreConversionHelper
    {
        private const double max_score = 1000000;

        public static double GetModMultiplier(ScoreConverter converter)
        {
            ScoreInfo score = converter.ScoreInfo;
            IBeatmap beatmap = converter.Beatmap.Beatmap;

            var scoreMultiplierCalculator = new OsuScoreMultiplierCalculatorV2(new ScoreMultiplierContext(beatmap.Difficulty, score));
            return scoreMultiplierCalculator.CalculateFor(score.Mods);
        }

        public static long GetBonusScore(ScoreConverter converter)
        {
            if (converter.Source != ScoreType.Lazer)
                return 0;

            var statistics = converter.ScoreInfo.Statistics;

            statistics.TryGetValue(HitResult.SmallBonus, out int smallBonus);
            statistics.TryGetValue(HitResult.LargeBonus, out int largeBonus);

            return smallBonus * 10L + largeBonus * 50L;
        }

        public static double GetUniversalComboScore(ScoreConverter converter)
        {
            if (converter.Source != ScoreType.Lazer)
                throw new ArgumentException("Expected a lazer source score.", nameof(converter));

            double accuracy = converter.ScoreInfo.Accuracy;

            if (accuracy <= 0 || converter.ScoreInfo.MaxCombo <= 0)
                return 0;

            double scoreWithoutBonus = converter.ScoreInfo.TotalScoreWithoutMods - GetBonusScore(converter);
            double lazerCombo = (2 * scoreWithoutBonus / max_score - Math.Pow(accuracy, 5)) / accuracy;

            return ComboScoreConversionHelper.LazerToLinear(converter, lazerCombo);
        }

        public static long CalculateScore(ScoreConverter converter)
        {
            double accuracy = converter.ScoreInfo.Accuracy;
            double lazerCombo = ComboScoreConversionHelper.LinearToLazer(converter, converter.UniversalComboScore);

            double scoreWithoutMods = max_score * 0.5 * (Math.Pow(accuracy, 5) + accuracy * lazerCombo);

            // Bonus intentionally excluded from the converted score.
            return (long)Math.Round(scoreWithoutMods * GetModMultiplier(converter));
        }

    }
}
