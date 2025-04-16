using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
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
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private CollectionManager collections { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

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
                                        activeCollectionButton = new RoundedButton()
                                        {
                                            Width = 250,
                                            Height = collection_controls_height,
                                            Text = "Select as active collection",
                                            BackgroundColour = colourProvider.Background1,
                                            Action = selectAsActiveCollection
                                        },
                                        new StatefulButton("Overwrite pp values")
                                        {
                                            Width = 200,
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
                                                        drawableScore.ChangeLivePp(profileScore.PerformanceAttributes.Total);
                                                    }

                                                    collections.Save();
                                                }));
                                            }
                                        },
                                        new StatefulButton("Clear collection")
                                        {
                                            Width = 150,
                                            Height = collection_controls_height,
                                            BackgroundColour = colourProvider.Background1,
                                            Action = () =>
                                            {
                                                dialogOverlay.Push(new ConfirmDialog("Do you really want to delete all scores in this collection?", () =>
                                                {
                                                    currentCollection.Scores.Clear();
                                                    drawableScores.Clear();
                                                    collections.Save();
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

            populateCollectionsContainer();

            collections.Collections.CollectionChanged += (sender, e) => populateCollectionsContainer();

            ruleset.ValueChanged += _ => performCalculation();
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
                activeCollectionButton.Text = "This collection is active";
            }
            else
            {
                activeCollectionButton.BackgroundColour = colourProvider.Background1;
                activeCollectionButton.Text = "Select as active collection";
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
                        collections.Collections.Add(new Collection("New Collection", 0));
                        collections.Save();
                    }
                });
            });
        }

        private void openCollection(Collection collection)
        {
            collectionsViewContainer.Hide();
            collectionContainer.Show();

            // Unsubscribe the collection changed event handler from the previously opened collection
            //if (currentCollection != null)
            //    currentCollection.Scores.CollectionChanged -= collectionChangedEventHandler;

            currentCollection = collection;
            collectionNameText.Text = collection.Name.Value;

            // Store the event handler to unsubscribe when opening a different collection
            //collectionChangedEventHandler = (sender, e) =>
            //{
            //    if (e.Action == NotifyCollectionChangedAction.Add)
            //        performCalculation(e.NewItems.Cast<ScoreInfo>(), false);
            //    else if (e.Action == NotifyCollectionChangedAction.Remove)
            //        drawableScores.RemoveAll(x => e.OldItems.Cast<long>().Contains(x.Score.SoloScore.OnlineID), true);
            //};

            if (collections.ActiveCollection == null) selectAsActiveCollection();
            else updateActiveCollectionButton();

            collection.Scores.CollectionChanged += collectionChangedEventHandler;

            performCalculation();
        }

        private void performCalculation(IEnumerable<ScoreInfo> scores = null, bool overwritePp = false)
        {
            if (currentCollection == null)
                return;

            scores ??= currentCollection.Scores;

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = new CancellationTokenSource();

            loadingLayer.Show();

            Task.Run(async () =>
            {
                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (ScoreInfo score in scores)
                {
                    if (calculationCancellatonToken.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapInfo.OnlineID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    Mod[] mods = score.Mods;

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
                    DrawableExtendedProfileScore[] sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total - ((ExtendedProfileScore)x.Score).LivePP).ToArray();

                    for (int i = 0; i < sortedScores.Length; i++)
                        drawableScores.SetLayoutPosition(sortedScores[i], i);
                });

            }, calculationCancellatonToken.Token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() => loadingLayer.Hide());
            });
        }

        private void addScoreToUI(ExtendedProfileScore score)
        {
            Schedule(() =>
            {
                DrawableExtendedProfileScore drawable = new DrawableExtendedProfileScore(score);
                drawable.PopoverMaker = () => new CollectionScreenScorePopover(this, drawable);

                drawableScores.Add(drawable);
            });
        }

        public void DeleteScoreFromCollection(DrawableExtendedProfileScore drawableScore)
        {
            currentCollection.Scores.Remove(drawableScore.Score.ScoreInfoSource);
            collections.Save();
            drawableScores.Remove(drawableScore, true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape)
            {
                calculationCancellatonToken?.Cancel();
                collectionContainer.Hide();
                collectionsViewContainer.Show();
                currentCollection = null;
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

        private partial class CollectionScreenScorePopover : OsuPopover
        {
            [Resolved]
            private DialogOverlay dialogOverlay { get; set; }

            private readonly CollectionsScreen parent;
            private readonly DrawableExtendedProfileScore drawableScore;

            public CollectionScreenScorePopover(CollectionsScreen parent, DrawableExtendedProfileScore drawableScore)
            {
                this.parent = parent;
                this.drawableScore = drawableScore;
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
                            Children = new Drawable[]
                            {
                                new RoundedButton
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
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}
