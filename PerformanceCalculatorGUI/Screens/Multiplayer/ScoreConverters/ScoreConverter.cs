// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    public class ScoreConverter
    {
        public ScoreType Source { get; private set; }
        public double UniversalComboScore { get; private set; }
        public ScoreInfo ScoreInfo { get; private set; }
        public WorkingBeatmap Beatmap { get; private set; }
        public long TotalScore => ScoreInfo.TotalScore;

        public ScoreConverter(SettingsManager configManager, SoloScoreInfo score, RoomBeatmap roomBeatmap, ScoreType source)
        {
            long beatmapId = score.BeatmapID > 0 ? score.BeatmapID : roomBeatmap.Id;

            if (beatmapId <= 0)
                throw new ArgumentException("The score has no valid beatmap ID.");

            Beatmap = ProcessorWorkingBeatmap.FromFileOrId(beatmapId.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

            var ruleset = RulesetHelper.GetRulesetFromLegacyID(score.RulesetID);
            var mods = Array.ConvertAll(score.Mods, mod => mod.ToMod(ruleset));

            ScoreInfo = score.ToScoreInfo(mods, Beatmap.BeatmapInfo);
            ScoreInfo.Ruleset = ruleset.RulesetInfo;
            ScoreInfo.Statistics = new(score.Statistics);
            ScoreInfo.MaximumStatistics = new(score.MaximumStatistics);

            Source = source;

            UniversalComboScore = source switch
            {
                ScoreType.Lazer => LazerScoreConversionHelper.GetUniversalComboScore(this),
                ScoreType.Legacy => 0,
                ScoreType.ScoreV2 => ScoreV2ConversionHelper.GetUniversalComboScore(this),
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
            };
        }

        public long GetScoreForType(ScoreType type)
        {
            if (type == Source || type == ScoreType.None)
                return TotalScore;

            return type switch
            {
                ScoreType.Lazer => LazerScoreConversionHelper.CalculateScore(this),
                ScoreType.Legacy => 0,
                ScoreType.ScoreV2 => ScoreV2ConversionHelper.CalculateScore(this),
                ScoreType.LazerCustom => getCustomLazerScore(),
                ScoreType.PP => (long)Math.Round(ScoreInfo.PP ?? calculatePerformance()),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private long getCustomLazerScore()
        {
            long lazerScore = Source == ScoreType.Lazer ? TotalScore : LazerScoreConversionHelper.CalculateScore(this);

            if (ScoreInfo.Mods.Any(m => m is ModNoFail))
                lazerScore *= 2;

            double normalizedScore = (double)lazerScore / 1_000_000;

            double adjustedScore = 1_000_000 * (normalizedScore * normalizedScore);
            return (long)Math.Round(adjustedScore);
        }

        private double calculatePerformance()
        {
            var ruleset = RulesetHelper.GetRulesetFromLegacyID(ScoreInfo.RulesetID);

            var difficultyCalculator = ruleset.CreateDifficultyCalculator(Beatmap);
            var difficultyAttributes = difficultyCalculator.Calculate(ScoreInfo.Mods);

            var performanceCalculator = ruleset.CreatePerformanceCalculator();
            if (performanceCalculator == null) return 0;

            var performanceAttributes = performanceCalculator.Calculate(ScoreInfo, difficultyAttributes);
            ScoreInfo.PP = performanceAttributes.Total;
            return ScoreInfo.PP.Value;
        }

        public static string FormatScore(long score, ScoreType type)
        {
            if (type == ScoreType.PP)
                return $"{score:N0}pp";
            return $"{score:N0}";
        }
    }
}
