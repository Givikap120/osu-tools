// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring.Legacy;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    public static class ScoreV2ConversionHelper
    {
        private const double max_score = 1000000;

        public static double GetModMultiplier(ScoreConverter converter)
        {
            var ruleset = new OsuRuleset();

            var mods = converter.ScoreInfo.Mods.ToArray(); // Need to copy

            if (!mods.Any(mod => mod is ModScoreV2))
            {
                mods = mods.Append(new ModScoreV2()).ToArray();
            }

            return ruleset.CreateLegacyScoreSimulator().GetLegacyScoreMultiplier(
                mods,
                new LegacyBeatmapConversionDifficultyInfo
                {
                    SourceRuleset = ruleset.RulesetInfo
                });
        }

        public static double GetUniversalComboScore(ScoreConverter converter)
        {
            if (converter.Source != ScoreType.ScoreV2)
                throw new ArgumentException(
                    "Expected a ScoreV2 source score.", nameof(converter));

            double accuracy = converter.ScoreInfo.Accuracy;
            double modMultiplier = GetModMultiplier(converter);

            // Prefer the original stable score when supplied by the API.
            // Otherwise TotalScore must contain the original ScoreV2 score.
            double originalScore =
                converter.ScoreInfo.LegacyTotalScore
                ?? converter.ScoreInfo.TotalScore;

            double scoreWithoutMods = originalScore / modMultiplier;

            double weightedComboPortion =
                (scoreWithoutMods / max_score
                 - 0.3 * Math.Pow(accuracy, 10))
                / 0.7;

            // Approximate removal of judgement-quality weighting.
            return Math.Clamp(weightedComboPortion / accuracy, 0, 1);
        }

        public static long CalculateScore(ScoreConverter converter)
        {
            double accuracy = converter.ScoreInfo.Accuracy;
            double linearCombo =
                Math.Clamp(converter.UniversalComboScore, 0, 1);

            double accuracyPortion = Math.Pow(accuracy, 10);
            double weightedComboPortion = accuracy * linearCombo;

            double scoreWithoutMods = max_score *
                (0.7 * weightedComboPortion + 0.3 * accuracyPortion);

            // Bonus intentionally ignored.
            return (long)Math.Round(
                scoreWithoutMods * GetModMultiplier(converter));
        }
    }
}
