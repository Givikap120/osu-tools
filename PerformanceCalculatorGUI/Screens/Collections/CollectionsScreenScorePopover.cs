// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using PerformanceCalculatorGUI.Components.Scores;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public partial class CollectionsScreenScorePopover : ScorePopover
    {
        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        [Resolved]
        private CollectionManager collections { get; set; }

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
            if (collections.ActiveCollection != null && collections.ActiveCollection != parent.CurrentCollection) Buttons.Add(new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Text = "Add score to active collection",
                Action = () =>
                {
                    collections.ActiveCollection.Scores.Insert(0, Score.ScoreInfoSource); ;
                    collections.SaveCollections();
                    PopOut();
                }
            });

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
