// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using PerformanceCalculatorGUI.Configuration;
using PerformanceCalculatorGUI.Components.Scores;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays;
using AutoMapper.Internal;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace PerformanceCalculatorGUI.Screens.Profile
{
    public partial class ProfileScreenScorePopover : ScorePopover
    {
        [Resolved]
        private CollectionManager collections { get; set; }

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        private readonly ProfileScreen parent;

        public ProfileScreenScorePopover(ProfileScore score, ProfileScreen parent) : base(score)
        {
            this.parent = parent;
        }

        private Drawable[] getCollectionButtons()
        {
            if (collections.ActiveCollection == null) return Array.Empty<Drawable>();

            return
            [
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Add score to active collection",
                    Action = () =>
                    {
                        collections.ActiveCollection.Scores.Add(Score.ScoreInfoSource);
                        collections.SaveCollections();
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
                                collections.ActiveCollection.Scores.Add(score);
                            }

                            collections.SaveCollections();
                            PopOut();
                        }));
                    }
                },
            ];
        }


        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                AutoSizeAxes = Axes.Y,
                Width = 300,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Direction = FillDirection.Vertical,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(12),
                        Children = CreateScoreInfoButtons().Concat(getCollectionButtons()).ToArray()
                    }
                }
            });
        }
    }
}
