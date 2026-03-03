// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.Collections;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public partial class MyCollectionsScreen : PerformanceCalculatorScreen
    {
        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; } = null!;

        [Resolved]
        private SettingsManager configManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private CollectionManager collections { get; set; } = null!;

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; } = null!;

        public MyCollection? CurrentCollection { get; private set; } = null!;

        private VerboseLoadingLayer loadingLayer = null!;
        private FillFlowContainer collectionsViewContainer = null!;
        private GridContainer collectionContainer = null!;
        private SpriteText collectionNameText = null!;
        private FillFlowContainer<ExtendedProfileScore> drawableScores = null!;

        private CancellationTokenSource calculationCancellatonToken = null!;
        //private NotifyCollectionChangedEventHandler collectionChangedEventHandler = null!;

        private const float collection_controls_height = 40;

        private RoundedButton activeCollectionButton = null!;
        private AddScoresButton addScoresButton = null!;

        private OverlaySortTabControl<MyCollectionSortCriteria> sortingTabControl = null!;
        private readonly Bindable<MyCollectionSortCriteria> sorting = new Bindable<MyCollectionSortCriteria>(MyCollectionSortCriteria.Difference);

        private bool isCalculating = false;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = collectionsViewContainer = new FillFlowContainer
                    {
                        Margin = new MarginPadding(20),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full
                    }
                },
                collectionContainer = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new [] { new Dimension() },
                    RowDimensions = new []
                    {
                        new Dimension(GridSizeMode.Absolute, collection_controls_height),
                        new Dimension()
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                Height = collection_controls_height,
                                RelativeSizeAxes = Axes.X,
                                RowDimensions = new [] { new Dimension(GridSizeMode.Absolute, collection_controls_height) },
                                ColumnDimensions = new []
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        collectionNameText = new OsuSpriteText
                                        {
                                            Margin = new MarginPadding { Left = 8 },
                                            Font = new FontUsage(size: 28),
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft
                                        },
                                        new EmptyDrawable(),
                                        sortingTabControl = new OverlaySortTabControl<MyCollectionSortCriteria>
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Margin = new MarginPadding { Right = 22 },
                                            Current = { BindTarget = sorting },
                                            Alpha = 0
                                        },
                                        activeCollectionButton = new RoundedButton()
                                        {
                                            Width = 100,
                                            Height = collection_controls_height,
                                            Action = selectAsActiveCollection
                                        },
                                        new StatefulButton("Overwrite pp values")
                                        {
                                            Width = 150,
                                            Height = collection_controls_height,
                                            BackgroundColour = colourProvider.Background1,
                                            Action = () =>
                                            {
                                                dialogOverlay.Push(new ConfirmDialog("Do you really want to overwrite all pp values with local values?", () =>
                                                {
                                                    foreach(var drawableScore in drawableScores.Children)
                                                    {
                                                        var profileScore = drawableScore.Score;
                                                        var scoreInfo = profileScore.ScoreInfoSource!;
                                                        scoreInfo.PP = profileScore.PerformanceAttributes?.Total;
                                                        drawableScore.LivePP = profileScore.PerformanceAttributes?.Total ?? 0;
                                                    }

                                                    collections.SaveCollection(CurrentCollection!);
                                                }));
                                            }
                                        },
                                        addScoresButton = new AddScoresButton(this)
                                        {
                                            Width = 150,
                                            Height = collection_controls_height,
                                            Text = "Add Score",
                                            BackgroundColour = colourProvider.Background1,
                                            Action = () =>
                                            {
                                                addScoresButton.ShowPopover();
                                            }
                                        },
                                        new RoundedButton()
                                        {
                                            Width = 150,
                                            Height = collection_controls_height,
                                            Text = "Clear collection",
                                            BackgroundColour = colourProvider.Background1,
                                            Action = () =>
                                            {
                                                dialogOverlay.Push(new ConfirmDialog("Do you really want to delete all scores in this collection?", () =>
                                                {
                                                    CurrentCollection?.Scores.Clear();
                                                    drawableScores.Clear();
                                                    collections.SaveCollection(CurrentCollection!);
                                                }));
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer(Direction.Vertical)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = drawableScores = new FillFlowContainer<ExtendedProfileScore>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical
                                }
                            }
                        }
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            collectionContainer.Hide();

            sorting.ValueChanged += e => { updateSorting(e.NewValue); };

            populateCollectionsContainer();

            collections.Collections.CollectionChanged += (sender, e) => populateCollectionsContainer();
        }

        private void selectAsActiveCollection()
        {
            collections.ActiveCollection = CurrentCollection;
            updateActiveCollectionButton();
        }

        private void updateActiveCollectionButton()
        {
            if (collections.ActiveCollection == CurrentCollection)
            {
                activeCollectionButton.BackgroundColour = colours.Green;
                activeCollectionButton.Text = "Active";
            }
            else
            {
                activeCollectionButton.BackgroundColour = colourProvider.Background1;
                activeCollectionButton.Text = "Select";
            }
        }

        private void populateCollectionsContainer()
        {
            Schedule(() =>
            {
                collectionsViewContainer.Clear();

                foreach (MyCollection collection in collections.Collections)
                    collectionsViewContainer.Add(new CollectionCard(collection) { Action = () => openCollection(collection) });

                collectionsViewContainer.Add(new CollectionCard()
                {
                    Action = () =>
                    {
                        var newCollection = new MyCollection("New Collection", 0, ruleset.Value.OnlineID);
                        collections.Collections.Add(newCollection);
                        collections.SaveCollection(newCollection);
                    }
                });
            });
        }

        private void openCollection(MyCollection collection)
        {
            collectionsViewContainer.Hide();
            collectionContainer.Show();

            CurrentCollection = collection;
            collectionNameText.Text = collection.Name.Value;

            if (collections.ActiveCollection == null) selectAsActiveCollection();
            else updateActiveCollectionButton();

            //collection.Scores.CollectionChanged += collectionChangedEventHandler;

            ruleset.Value = RulesetHelper.GetRulesetFromLegacyID(collection.RulesetId).RulesetInfo;
            performCalculation();
        }

        private void performCalculation()
        {
            if (CurrentCollection == null || isCalculating)
                return;

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = new CancellationTokenSource();

            loadingLayer.Show();
            isCalculating = true;

            Task.Run(async () =>
            {
                Schedule(() =>
                {
                    sortingTabControl.Alpha = 1.0f;
                    sortingTabControl.Current.Value = MyCollectionSortCriteria.Difference;
                    drawableScores.Clear();
                });

                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (CollectionScore score in CurrentCollection.Scores)
                {
                    if (calculationCancellatonToken.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.ScoreInfo.BeatmapInfo!.OnlineID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);
                    score.ScoreInfo.BeatmapInfo = working.BeatmapInfo;

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    var mods = score.ScoreInfo.Mods;

                    Score parsedScore = new ProcessorScoreDecoder(working).Parse(score.ScoreInfo);

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(mods);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                    if (calculationCancellatonToken.IsCancellationRequested)
                        return;

                    // Recalculate accuracy to make CL have correct values
                    IBeatmap? beatmap = (IBeatmap?)difficultyCalculator?.GetType()?.GetProperty("Beatmap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(difficultyCalculator);
                    if (beatmap != null) parsedScore.ScoreInfo.Accuracy = RulesetHelper.GetAccuracyForRuleset(ruleset.Value, beatmap, parsedScore.ScoreInfo.Statistics, parsedScore.ScoreInfo.Mods);

                    double livePP = score.ScoreInfo.PP ?? 0.0;
                    var perfAttributes = await (performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, calculationCancellatonToken.Token))!.ConfigureAwait(false);

                    addScoreToUI(new ExtendedScore(score.ScoreInfo, livePP, difficultyAttributes, perfAttributes), score);
                }

                Schedule(() =>
                {
                    updateSorting(sorting.Value);
                });

            }, calculationCancellatonToken.Token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
                isCalculating = false;
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() => loadingLayer.Hide());
                isCalculating = false;
            });
        }

        private void addScoreToUI(ExtendedScore score, CollectionScore collectionScore)
        {
            Schedule(() =>
            {
                var drawable = new ExtendedProfileScore(score) { DifferenceMode = sorting.Value.GetDifferenceMode() };
                drawable.PopoverMaker = () => new CollectionsScreenScorePopover(this, drawable, collectionScore);

                drawableScores.Add(drawable);
            });
        }

        public void DeleteScoreFromCollection(ExtendedProfileScore drawableScore)
        {
            CurrentCollection!.Scores.RemoveAll(s => s.ScoreInfo == drawableScore.Score.ScoreInfoSource);
            collections.SaveCollection(CurrentCollection);
            drawableScores.Remove(drawableScore, true);
        }

        private void updateSorting(MyCollectionSortCriteria sortCriteria)
        {
            if (!drawableScores.Children.Any())
                return;

            ExtendedProfileScore[] sortedScores;

            switch (sortCriteria)
            {
                case MyCollectionSortCriteria.Index:
                    var scoreInfos = CurrentCollection?.Scores.Select(s => s.ScoreInfo).ToList();
                    sortedScores = drawableScores.Children.OrderBy(x => scoreInfos?.IndexOf(x.Score.ScoreInfoSource!)).ToArray();
                    break;

                case MyCollectionSortCriteria.Name:
                    sortedScores = drawableScores.Children.OrderBy(x => x.Score.ScoreInfoSource?.BeatmapInfo?.Metadata.Title).ToArray();
                    break;

                case MyCollectionSortCriteria.Live:
                    sortedScores = drawableScores.Children.OrderByDescending(x => (x.Score).LivePP).ToArray();
                    break;

                case MyCollectionSortCriteria.Local:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes?.Total).ToArray();
                    break;

                case MyCollectionSortCriteria.StarRating:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.DifficultyAttributes.StarRating).ToArray();
                    break;

                case MyCollectionSortCriteria.Difference:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes?.Total - (x.Score).LivePP).ToArray();
                    break;

                case MyCollectionSortCriteria.Percentage:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes?.Total / (x.Score).LivePP).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortCriteria), sortCriteria, null);
            }

            DifferenceMode differenceMode = sortCriteria.GetDifferenceMode();

            for (int i = 0; i < sortedScores.Length; i++)
            {
                drawableScores.SetLayoutPosition(sortedScores[i], i);
                sortedScores[i].DifferenceMode = differenceMode;
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape)
            {
                calculationCancellatonToken?.Cancel();

                if (!isCalculating)
                {
                    collectionContainer.Hide();
                    collectionsViewContainer.Show();
                    CurrentCollection = null;
                }
            }

            return base.OnKeyDown(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
        }

        private partial class EmptyDrawable : Drawable
        {

        }

        private partial class AddScoresButton : RoundedButton, IHasPopover
        {
            private MyCollectionsScreen parent;

            public AddScoresButton(MyCollectionsScreen parent)
            {
                this.parent = parent;
            }

            public Popover GetPopover() => new CollectionsScreenAddScorePopover(parent.CurrentCollection);
        }
    }
}
