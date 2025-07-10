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

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public partial class CollectionsScreenAddScorePopover : OsuPopover
    {
        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private CollectionManager collections { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        private LabelledTextBox scoreIdTextBox;
        private SwitchButton legasyScoreSwitch;
        private StatefulButton addScoreButton;

        private void tryAddScoreFromId(string scoreId)
        {
            if (addScoreButton.State.Value == ButtonState.Loading)
                return;

            addScoreButton.State.Value = ButtonState.Loading;

            Task.Run(async () =>
            {
                var soloScoreInfo = await apiManager.GetJsonFromApi<SoloScoreInfo>($"scores/{scoreId}").ConfigureAwait(false);
                var beatmap = ProcessorWorkingBeatmap.FromFileOrId(soloScoreInfo.BeatmapID.ToString(), null, configManager.GetBindable<string>(Settings.CachePath).Value);
                var score = soloScoreInfo.ToScoreInfo(rulesets, beatmap?.BeatmapInfo);
                collections.ActiveCollection.Scores.Insert(0, score);
                collections.SaveCollections();
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
