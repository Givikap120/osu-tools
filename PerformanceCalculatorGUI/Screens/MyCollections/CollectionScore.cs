// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public enum PpTarget
    {
        Master,
        Branch
    }

    public struct CollectionScore
    {
        public ScoreInfo ScoreInfo;

        public double MasterPp = -1; // 
        public double BranchPp = -1;
        public double DeltaPp = -1;

        public CollectionScore(ScoreInfo score) { ScoreInfo = score; }

        public CollectionScore(ScoreInfo score, PpTarget target)
        {
            ScoreInfo = score;
            if (target == PpTarget.Master) MasterPp = score.PP ?? -1;
            else if (target == PpTarget.Branch) BranchPp = score.PP ?? -1;
        }
    }
}
