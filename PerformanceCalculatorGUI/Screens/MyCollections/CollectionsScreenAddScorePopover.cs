// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using PerformanceCalculatorGUI.Components;
using System.Threading.Tasks;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using PerformanceCalculatorGUI.Configuration;
using osu.Framework.Logging;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public partial class CollectionsScreenAddScorePopover : OsuPopover
    {
        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; } = null!;

        [Resolved]
        private APIManager apiManager { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private ScreenCollectionManager collections { get; set; } = null!;

        [Resolved]
        private SettingsManager configManager { get; set; } = null!;

        private LabelledTextBox scoreIdTextBox = null!;
        private StatefulButton addScoreButton = null!;

        private MyCollection? currentCollection;

        public CollectionsScreenAddScorePopover(MyCollection? currentCollection)
        {
            this.currentCollection = currentCollection;
        }

        private void tryAddScoreFromId(string scoreId)
        {
            if (addScoreButton.State.Value == ButtonState.Loading)
                return;

            addScoreButton.State.Value = ButtonState.Loading;

            Task.Run(async () =>
            {
                if (currentCollection == null) return;

                var soloScoreInfo = await apiManager.GetJsonFromApi<SoloScoreInfo>($"scores/{scoreId}").ConfigureAwait(false);
                var beatmap = ProcessorWorkingBeatmap.FromFileOrId(soloScoreInfo.BeatmapID.ToString(), null, configManager.GetBindable<string>(Settings.CachePath).Value);
                var score = soloScoreInfo.ToScoreInfo(rulesets, beatmap?.BeatmapInfo);

                currentCollection.Scores.Insert(0, new CollectionScore(score, PpTarget.Master));
                collections.SaveCollection(currentCollection);
            }).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    addScoreButton.State.Value = ButtonState.Done;
                });
            });
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
                        Children =
                        [
                            scoreIdTextBox = new LabelledTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                                Label = "Score ID",
                                PlaceholderText = "0 or osu/0",
                            },
                            addScoreButton = new StatefulButton("Add score")
                            {
                                RelativeSizeAxes = Axes.X,
                                Action = () =>
                                {
                                    if (RulesetHelper.ValidateScoreId(scoreIdTextBox.Current.Value))
                                    {
                                        tryAddScoreFromId(scoreIdTextBox.Current.Value);
                                    }
                                    else
                                    {
                                        notificationDisplay.Display(new Notification("Incorrect score id"));
                                    }
                                }
                            }
                        ]
                    }
                }
            });
        }
    }
}
