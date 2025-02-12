// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceCalculatorGUI
{
    public interface IExtendedDifficultyCalculator
    {
        Skill[] GetSkills();

        DifficultyHitObject[] GetDifficultyHitObjects(IBeatmap beatmap, double clockRate);

        static DifficultyHitObject[] GetDifficultyHitObjects(DifficultyCalculator difficultyCalculator, IBeatmap beatmap, double clockRate)
        {
            MethodInfo methodInfo = difficultyCalculator.GetType().GetMethod("CreateDifficultyHitObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo != null)
            {
                return ((IEnumerable<DifficultyHitObject>)methodInfo.Invoke(difficultyCalculator, new object[] { beatmap, clockRate })).ToArray();
            }

            throw new InvalidOperationException("Method not found");

        }
    }
}
