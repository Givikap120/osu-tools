// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;

namespace PerformanceCalculatorGUI.Components.BeatmapDataExport
{
    public struct RelevantBaseData
    {
        // Default data
        public double Length;
        public double BPM;
        public int CircleCount;
        public int SliderCount;

        // Attributes data
        public double CircleSize;
        public double ApproachRate;
        public double OverallDifficulty;

        // Diffcalc data
        public double StarRating;
        public double AimDifficulty;
        public double SpeedDifficulty;
        public double FlashlightDifficulty;
        public double SliderFactor;

        public RelevantBaseData(WorkingBeatmap beatmap, OsuDifficultyAttributes osuDifficultyAttributes)
        {
            Length = beatmap.Beatmap.CalculatePlayableLength() / 1000;
            BPM = 60000 / beatmap.Beatmap.GetMostCommonBeatLength();

            CircleCount = osuDifficultyAttributes.HitCircleCount;
            SliderCount = osuDifficultyAttributes.SliderCount;

            CircleSize = beatmap.BeatmapInfo.Difficulty.CircleSize;
            ApproachRate = beatmap.BeatmapInfo.Difficulty.ApproachRate;
            OverallDifficulty = beatmap.BeatmapInfo.Difficulty.OverallDifficulty;

            StarRating = osuDifficultyAttributes.StarRating;
            AimDifficulty = osuDifficultyAttributes.AimDifficulty;
            SpeedDifficulty = osuDifficultyAttributes.SpeedDifficulty;

            var type = osuDifficultyAttributes.GetType();

            FlashlightDifficulty =
                (double?)type.GetProperty("FlashlightDifficulty")?.GetValue(osuDifficultyAttributes)
                ?? 0;

            SliderFactor =
                (double?)type.GetProperty("SliderFactor")?.GetValue(osuDifficultyAttributes)
                ?? 0;
        }

        public override readonly string ToString()
        {
            //return $"{Length},{BPM},{CircleCount},{SliderCount},{CircleSize},{ApproachRate},{OverallDifficulty},{StarRating},{AimDifficulty},{SpeedDifficulty},{FlashlightDifficulty},{SliderFactor}";
            return $"{Length},{BPM},{CircleCount},{SliderCount},{CircleSize},{ApproachRate},{OverallDifficulty},{StarRating},{AimDifficulty},{SpeedDifficulty},{SliderFactor}";
        }

        public static string GetHeader()
        {
            //return "Length,BPM,CircleCount,SliderCount,CircleSize,ApproachRate,OverallDifficulty,StarRating,AimDifficulty,SpeedDifficulty,FlashlightDifficulty,SliderFactor";
            return "Length,BPM,CircleCount,SliderCount,CircleSize,ApproachRate,OverallDifficulty,StarRating,AimDifficulty,SpeedDifficulty,SliderFactor";
        }
    }
}
