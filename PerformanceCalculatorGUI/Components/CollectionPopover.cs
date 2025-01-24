// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osuTK;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public partial class CollectionPopover : OsuPopover
    {
        [Resolved]
        private CollectionManager collections { get; set; }

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        private LabelledTextBox nameTextBox;
        private LabelledNumberBox coverBeatmapSetIdTextBox;

        private readonly Collection collection;


        public CollectionPopover(Collection collection)
        {
            this.collection = collection;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
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
                        Children = new Drawable[]
                        {
                            nameTextBox = new LabelledTextBox
                            {
                                Label = "Name",
                                Text = collection.Name.Value,
                                Current = collection.Name
                            },
                            coverBeatmapSetIdTextBox = new LabelledNumberBox
                            {
                                Label = "Cover Beatmap Set ID",
                                Text = collection.CoverBeatmapSetId.Value,
                                Current = collection.CoverBeatmapSetId
                            },
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = "Delete",
                                BackgroundColour = colours.DangerousButtonColour,
                                Action = () =>
                                {
                                    dialogOverlay.Push(new ConfirmDialog("Are you sure?", () =>
                                    {
                                        collections.Collections.Remove(collection);
                                        collections.Save();
                                    }));
                                }
                            }
                        }
                    }
                }
            });

            nameTextBox.OnCommit += (sender, e) =>
            {
                collections.Save();
            };

            coverBeatmapSetIdTextBox.OnCommit += (sender, e) =>
            {
                collections.Save();
            };
        }
    }
}
