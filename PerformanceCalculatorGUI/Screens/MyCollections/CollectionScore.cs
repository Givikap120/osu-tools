// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using osu.Game.Scoring;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public enum PpTarget
    {
        Master,
        Branch
    }

    public class CollectionScore : ScoreInfo
    {
        // Not used for now, everything is based of normal "PP" member
        public double MasterPP = 0;
        public double BranchPP = 0;

        // Used in delta mode for calculating expected deltas
        public double DeltaPercentage = 1;

        public CollectionScore(ScoreInfo score)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Copy fields
            foreach (var field in typeof(ScoreInfo).GetFields(flags))
            {
                if (field.IsStatic)
                    continue;

                field.SetValue(this, field.GetValue(score));
            }

            // Copy properties
            foreach (var prop in typeof(ScoreInfo).GetProperties(flags))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (prop.GetIndexParameters().Length > 0) // skip indexers
                    continue;

                prop.SetValue(this, prop.GetValue(score));
            }
        }

        public CollectionScore(ScoreInfo score, PpTarget target) : this(score)
        {
            if (target == PpTarget.Master) MasterPP = score.PP ?? -1;
            else if (target == PpTarget.Branch) BranchPP = score.PP ?? -1;
        }
    }
}
