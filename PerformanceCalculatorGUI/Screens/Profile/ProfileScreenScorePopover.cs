// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Profile
{
    public partial class ProfileScreenScorePopover : ScorePopover
    {
        [Resolved]
        private ScreenCollectionManager collections { get; set; }

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        private readonly ProfileScreen parent;

        public ProfileScreenScorePopover(ExtendedScore score, ProfileScreen parent) : base(score)
        {
            this.parent = parent;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (collections.ActiveCollection == null) return;

            Buttons.AddRange([
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Add score to active collection",
                    Action = () =>
                    {
                        collections.ActiveCollection.Scores.Insert(0, new CollectionScore(Score.ScoreInfoSource, PpTarget.Master));
                        collections.SaveCollection(collections.ActiveCollection);
                        PopOut();
                    }
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Add all player scores to active collection",
                    Action = () =>
                    {
                        dialogOverlay.Push(new ConfirmDialog("Do you really want to add ALL player scores to collection?", () =>
                        {
                            var allScores = parent.GetProfileScores();

                            foreach (var score in allScores)
                            {
                                collections.ActiveCollection.Scores.Add(new CollectionScore(score, PpTarget.Master));
                            }

                            collections.SaveCollection(collections.ActiveCollection);
                            PopOut();
                        }));
                    }
                },
            ]);
        }
    }
}
