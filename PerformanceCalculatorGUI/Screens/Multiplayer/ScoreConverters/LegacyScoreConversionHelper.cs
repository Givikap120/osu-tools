// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring.Legacy;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    public static class LegacyScoreConversionHelper
    {
        private sealed class Parameters
        {
            public double AccuracyScore;
            public double ComboScore;
            public double ModMultiplier;
        }

        // Each converter must retain its original score, map and mods.
        private static readonly ConditionalWeakTable<ScoreConverter, Parameters> parameters = new();

        private static Parameters getParameters(ScoreConverter converter)
            => parameters.GetValue(converter, c =>
            {
                if (c.ScoreInfo.Ruleset.OnlineID != 0)
                    throw new NotSupportedException("Legacy score conversion supports osu!standard only.");

                var ruleset = new OsuRuleset();

                // Filter into a new array. Never change the source score's mods.
                var mods = c.ScoreInfo.Mods.Where(mod => mod is not ModScoreV2).ToArray();
                var playable = c.Beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

                if (playable.HitObjects.Count == 0)
                    throw new InvalidOperationException("The beatmap contains no hit objects.");

                var simulator = ruleset.CreateLegacyScoreSimulator();
                var attributes = simulator.Simulate(c.Beatmap, playable);
                double multiplier = simulator.GetLegacyScoreMultiplier(
                    mods,
                    LegacyBeatmapConversionDifficultyInfo.FromBeatmap(c.Beatmap.Beatmap));

                return new Parameters
                {
                    AccuracyScore = attributes.AccuracyScore,
                    ComboScore = Math.Round(attributes.ComboScore * multiplier),
                    ModMultiplier = multiplier
                };
            });

        public static double GetModMultiplier(ScoreConverter converter)
            => getParameters(converter).ModMultiplier;

        public static double GetUniversalComboScore(ScoreConverter converter)
        {
            if (converter.Source != ScoreType.Legacy)
                throw new ArgumentException("Expected a legacy source score.", nameof(converter));

            var p = getParameters(converter);
            double accuracy = Math.Clamp(converter.ScoreInfo.Accuracy, 0, 1);

            if (accuracy <= 0 || converter.ScoreInfo.MaxCombo <= 0)
                return 0;

            if (p.ComboScore <= 0)
                throw new InvalidOperationException(
                    "Cannot recover combo from a legacy score with no combo scoring component.");

            // TotalScore must still contain the original stable total if
            // LegacyTotalScore is unavailable. Do not use a converted total here.
            double originalScore = converter.ScoreInfo.LegacyTotalScore ?? converter.ScoreInfo.TotalScore;

            double estimatedBaseScore = p.AccuracyScore * accuracy;
            double weightedComboScore = Math.Max(0, originalScore - estimatedBaseScore);

            // Bonus is ignored: any unknown source bonus remains in this residual.
            // Remove average judgement quality to obtain the linear combo estimate
            // expected by ComboScoreConversionHelper.LinearToLazer().
            return Math.Clamp(weightedComboScore / p.ComboScore / accuracy, 0, 1);
        }

        public static long CalculateScore(ScoreConverter converter)
        {
            var p = getParameters(converter);
            double accuracy = Math.Clamp(converter.ScoreInfo.Accuracy, 0, 1);
            double linearCombo = Math.Clamp(converter.UniversalComboScore, 0, 1);

            // ScoreV1's multiplier applies to the combo component, not the entire total.
            // AccuracyScore also includes non-bonus slider elements; multiplying it
            // by mean accuracy is the same approximation used in lazer migration.
            double estimatedBaseScore = p.AccuracyScore * accuracy;
            double estimatedComboScore = p.ComboScore * accuracy * linearCombo;

            // No spinner bonus is added to converted scores.
            return checked((long)Math.Round(estimatedBaseScore + estimatedComboScore));
        }
    }
}
