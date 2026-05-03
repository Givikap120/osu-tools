// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Utils;

namespace PerformanceCalculatorGUI.Components.BeatmapDataExport
{
    public struct RelevantModData
    {
        public double StarRating;
        public double Rate = 1.0;

        // Those should be always set
        public double CircleSize = double.NaN;
        public double ApproachRate = double.NaN;
        public double OverallDifficulty = double.NaN;

        // WARNING: this should be bools, but I would use integer for easier export into the csv
        public int Hidden = 0;
        public int Flashlight = 0;
        public int Relax = 0;
        public int Autopilot = 0;

        public RelevantModData(BeatmapDifficulty baseDifficulty, Mod[] mods, double starRating)
        {
            StarRating = starRating;
            Rate = ModUtils.CalculateRateWithMods(mods);

            BeatmapDifficulty adjustedDifficulty = new BeatmapDifficulty(baseDifficulty);
            mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(adjustedDifficulty));
            CircleSize = adjustedDifficulty.CircleSize;
            ApproachRate = adjustedDifficulty.ApproachRate;
            OverallDifficulty = adjustedDifficulty.OverallDifficulty;

            Hidden = mods.OfType<OsuModHidden>().Any() ? 1 : 0;
            Flashlight = mods.OfType<OsuModFlashlight>().Any() ? 1 : 0;
            Relax = mods.OfType<OsuModRelax>().Any() ? 1 : 0;
            Autopilot = mods.OfType<OsuModAutopilot>().Any() ? 1 : 0;
        }

        public override readonly string ToString()
        {
            return $"{StarRating},{Rate},{CircleSize},{ApproachRate},{OverallDifficulty},{Hidden},{Flashlight},{Relax},{Autopilot}";
        }

        public static string GetHeader()
        {
            return "ModStarRating,Rate,ModCircleSize,ModApproachRate,ModOverallDifficulty,Hidden,Flashlight,Relax,Autopilot";
        }
    }
}
