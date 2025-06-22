// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using PerformanceCalculatorGUI.Components.Scores;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public partial class CollectionsScreenScorePopover : ScorePopover
    {
        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        private readonly CollectionsScreen parent;
        private readonly DrawableExtendedProfileScore drawableScore;

        public CollectionsScreenScorePopover(CollectionsScreen parent, DrawableExtendedProfileScore drawableScore) : base(drawableScore.Score)
        {
            this.parent = parent;
            this.drawableScore = drawableScore;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Buttons.Add(new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Text = "Delete score from collection",
                Action = () =>
                {
                    dialogOverlay.Push(new ConfirmDialog("Are you sure?", () =>
                    {
                        parent.DeleteScoreFromCollection(drawableScore);
                    }));

                    PopOut();
                }
            });
        }
    }
}
