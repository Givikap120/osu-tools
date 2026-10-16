// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osuTK;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters
{
    public class ScoreConverter
    {
        public ScoreType Source { get; private set; }
        public double UniversalComboScore { get; private set; }
        public ScoreInfo ScoreInfo { get; private set; }
        public WorkingBeatmap Beatmap { get; private set; }
        public double TotalScore => ScoreInfo.TotalScore;

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

        public double GetScoreForType(ScoreType type)
        {
            if (type == Source)
                return TotalScore;

            return type switch
            {
                ScoreType.Lazer => LazerScoreConversionHelper.CalculateScore(this),
                ScoreType.Legacy => 0,
                ScoreType.ScoreV2 => ScoreV2ConversionHelper.CalculateScore(this),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
