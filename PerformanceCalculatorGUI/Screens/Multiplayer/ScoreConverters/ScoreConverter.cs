// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
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
        public double TotalScore => ScoreInfo.TotalScore;

        public ScoreConverter(SettingsManager configManager, RoomScore roomScore, RoomBeatmap roomBeatmap, ScoreType source)
        {
            long beatmapId = roomScore.BeatmapId > 0
                ? roomScore.BeatmapId
                : roomBeatmap.Id;

            if (beatmapId <= 0)
                throw new ArgumentException("The score has no valid beatmap ID.");

            Beatmap = ProcessorWorkingBeatmap.FromFileOrId(beatmapId.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

            var json = JObject.FromObject(roomScore);
            json["id"] = roomScore.Id ?? 0;
            json["beatmap_id"] = beatmapId;

            var apiScore = json.ToObject<SoloScoreInfo>()
                ?? throw new InvalidOperationException("Could not deserialize the score.");

            var ruleset = RulesetHelper.GetRulesetFromLegacyID(roomScore.RulesetId);
            var mods = Array.ConvertAll(apiScore.Mods, mod => mod.ToMod(ruleset));

            ScoreInfo = apiScore.ToScoreInfo(mods, Beatmap.BeatmapInfo);
            ScoreInfo.Ruleset = ruleset.RulesetInfo;

            Source = source;

            UniversalComboScore = source switch
            {
                ScoreType.Lazer => LazerScoreConversionHelper.GetUniversalComboScore(this),
                ScoreType.Legacy => 0,
                ScoreType.ScoreV2 => 0,
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
                ScoreType.ScoreV2 => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
