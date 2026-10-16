// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    // Adapted from lazer's standardised legacy score migration.
    internal static class ComboScoreConversionHelper
    {
        private sealed class Parameters
        {
            public double Longest;
            public double Remaining;
        }

        private static readonly ConditionalWeakTable<ScoreConverter, Parameters> parameters = new();

        private static Parameters getParameters(ScoreConverter converter) => parameters.GetValue(converter, c =>
        {
            if (c.ScoreInfo.Ruleset.OnlineID != 0)
                throw new NotSupportedException("Combo conversion supports osu!standard only.");

            var ruleset = new OsuRuleset();
            var playable = c.Beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, c.ScoreInfo.Mods);
            var attributes = ruleset.CreateLegacyScoreSimulator().Simulate(c.Beatmap, playable);
            int maximumCombo = attributes.MaxCombo;

            if (maximumCombo <= 0)
                throw new InvalidOperationException("The beatmap has no maximum combo.");

            c.ScoreInfo.Statistics.TryGetValue(HitResult.Miss, out int misses);
            double longest = Math.Clamp((double)c.ScoreInfo.MaxCombo, 0, maximumCombo);

            return new Parameters
            {
                Longest = longest / maximumCombo,
                Remaining = Math.Max(0, maximumCombo - longest - Math.Max(0, misses)) / maximumCombo
            };
        });

        public static double LinearToLazer(ScoreConverter converter, double linearCombo)
            => estimate(getParameters(converter), linearCombo);

        private static double estimate(Parameters p, double linearCombo)
        {
            if (p.Longest <= 0)
                return 0;

            double longestLinear = p.Longest * p.Longest;
            double longestLazer = Math.Pow(p.Longest, 1.5);

            // Longest-combo lower bound from the migration algorithm.
            double linear = Math.Max(Math.Clamp(linearCombo, 0, 1), longestLinear);

            double repetitions = Math.Floor(linear / longestLinear);
            double remainder = Math.Max(0, linear - repetitions * longestLinear);
            double scoreEstimate = repetitions * longestLazer + Math.Pow(remainder, 0.75);

            // Equivalent to R * sqrt((linear - longestLinear) / R), including R == 0.
            double objectEstimate = longestLazer
                + Math.Sqrt(p.Remaining * Math.Max(0, linear - longestLinear));

            scoreEstimate = Math.Clamp(scoreEstimate, 0, 1);
            objectEstimate = Math.Clamp(objectEstimate, 0, 1);

            double lower = Math.Min(scoreEstimate, objectEstimate);
            double upper = Math.Max(scoreEstimate, objectEstimate);

            return Math.Min(0.3 * lower + 0.7 * upper, 1.2 * (lower + upper) / 2);
        }

        public static double LazerToLinear(ScoreConverter converter, double lazerCombo)
        {
            var p = getParameters(converter);
            if (p.Longest <= 0)
                return 0;

            double target = Math.Clamp(lazerCombo, 0, 1);
            double lower = p.Longest * p.Longest;
            double upper = 1;

            // Inconsistent inputs or model limitations: select the nearest endpoint.
            // Saturated regions have no unique inverse.
            if (target <= estimate(p, lower))
                return lower;
            if (target > estimate(p, upper))
                return upper;

            for (int i = 0; i < 56; i++)
            {
                double middle = (lower + upper) / 2;
                if (estimate(p, middle) < target)
                    lower = middle;
                else
                    upper = middle;
            }

            return (lower + upper) / 2;
        }
    }
}
