using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
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
using PerformanceCalculatorGUI.Components.Scores;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public partial class CollectionsScreen : PerformanceCalculatorScreen
    {
        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private OsuColour colours { get ; set; }

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private CollectionManager collections { get; set; }

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        private VerboseLoadingLayer loadingLayer;
        private FillFlowContainer collectionsViewContainer;
        private GridContainer collectionContainer;
        private SpriteText collectionNameText;
        private FillFlowContainer<DrawableExtendedProfileScore> drawableScores;

        private CancellationTokenSource calculationCancellatonToken;
        private Collection currentCollection;
        private NotifyCollectionChangedEventHandler collectionChangedEventHandler;

        private const float collection_controls_height = 40;

        private RoundedButton activeCollectionButton;
        private AddScoresButton addScoresButton;

        private OverlaySortTabControl<CollectionSortCriteria> sortingTabControl;
        private readonly Bindable<CollectionSortCriteria> sorting = new Bindable<CollectionSortCriteria>(CollectionSortCriteria.Difference);

        private bool isCalculating = false;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
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
                                        sortingTabControl = new OverlaySortTabControl<CollectionSortCriteria>
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
                                                        var scoreInfo = profileScore.ScoreInfoSource;
                                                        scoreInfo.PP = profileScore.PerformanceAttributes.Total;
                                                        drawableScore.LivePP = profileScore.PerformanceAttributes?.Total ?? 0;
                                                    }

                                                    collections.SaveCollections();
                                                }));
                                            }
                                        },
                                        addScoresButton = new AddScoresButton()
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
                                                    currentCollection.Scores.Clear();
                                                    drawableScores.Clear();
                                                    collections.SaveCollections();
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
                                Child = drawableScores = new FillFlowContainer<DrawableExtendedProfileScore>
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
            collections.ActiveCollection = currentCollection;
            updateActiveCollectionButton();
        }

        private void updateActiveCollectionButton()
        {
            if (collections.ActiveCollection == currentCollection)
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

                foreach (Collection collection in collections.Collections)
                    collectionsViewContainer.Add(new CollectionCard(collection) { Action = () => openCollection(collection) });

                collectionsViewContainer.Add(new CollectionCard()
                {
                    Action = () =>
                    {
                        collections.Collections.Add(new Collection("New Collection", 0, ruleset.Value.OnlineID));
                        collections.SaveCollections();
                    }
                });
            });
        }

        private void openCollection(Collection collection)
        {
            collectionsViewContainer.Hide();
            collectionContainer.Show();

            currentCollection = collection;
            collectionNameText.Text = collection.Name.Value;

            if (collections.ActiveCollection == null) selectAsActiveCollection();
            else updateActiveCollectionButton();

            collection.Scores.CollectionChanged += collectionChangedEventHandler;

            ruleset.Value = RulesetHelper.GetRulesetFromLegacyID(collection.RulesetId).RulesetInfo;
            performCalculation();
        }

        private void performCalculation()
        {
            if (currentCollection == null || isCalculating)
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
                    sortingTabControl.Current.Value = CollectionSortCriteria.Difference;
                    drawableScores.Clear();
                });

                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (ScoreInfo score in currentCollection.Scores)
                {
                    if (calculationCancellatonToken.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapInfo.OnlineID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    var mods = score.Mods;

                    var parsedScore = new ProcessorScoreDecoder(working).Parse(score);

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(mods);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                    if (calculationCancellatonToken.IsCancellationRequested)
                        return;

                    double livePP = score.PP ?? 0.0;
                    var perfAttributes = await (performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, calculationCancellatonToken.Token)).ConfigureAwait(false)!;

                    addScoreToUI(new ExtendedProfileScore(score, livePP, difficultyAttributes, perfAttributes));
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

        private void addScoreToUI(ExtendedProfileScore score)
        {
            Schedule(() =>
            {
                var drawable = new DrawableExtendedProfileScore(score) { DifferenceMode = sorting.Value.GetDifferenceMode() };
                drawable.PopoverMaker = () => new CollectionsScreenScorePopover(this, drawable);

                drawableScores.Add(drawable);
            });
        }

        public void DeleteScoreFromCollection(DrawableExtendedProfileScore drawableScore)
        {
            currentCollection.Scores.Remove(drawableScore.Score.ScoreInfoSource);
            collections.SaveCollections();
            drawableScores.Remove(drawableScore, true);
        }

        private void updateSorting(CollectionSortCriteria sortCriteria)
        {
            if (!drawableScores.Children.Any())
                return;

            DrawableProfileScore[] sortedScores;

            switch (sortCriteria)
            {
                case CollectionSortCriteria.Index:
                    sortedScores = drawableScores.Children.OrderBy(x => currentCollection.Scores.IndexOf(x.Score.ScoreInfoSource)).ToArray();
                    break;

                case CollectionSortCriteria.Live:
                    sortedScores = drawableScores.Children.OrderByDescending(x => ((ExtendedProfileScore)x.Score).LivePP).ToArray();
                    break;

                case CollectionSortCriteria.Local:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total).ToArray();
                    break;

                case CollectionSortCriteria.Difference:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total - ((ExtendedProfileScore)x.Score).LivePP).ToArray();
                    break;

                case CollectionSortCriteria.Percentage:
                    sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total / ((ExtendedProfileScore)x.Score).LivePP).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortCriteria), sortCriteria, null);
            }

            DifferenceMode differenceMode = sortCriteria.GetDifferenceMode();

            for (int i = 0; i < sortedScores.Length; i++)
            {
                drawableScores.SetLayoutPosition(sortedScores[i], i);
                ((DrawableExtendedProfileScore)sortedScores[i]).DifferenceMode = differenceMode;
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
                    currentCollection = null;
                }
            }

            return base.OnKeyDown(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
            //calculationCancellatonToken?.Dispose();
        }

        private partial class EmptyDrawable : Drawable
        {

        }

        private partial class AddScoresButton : RoundedButton, IHasPopover
        {
            public Popover GetPopover() => new CollectionsScreenAddScorePopover();
        }
    }
}
