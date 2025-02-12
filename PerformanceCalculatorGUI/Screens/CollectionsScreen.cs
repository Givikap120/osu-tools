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
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Placeholders;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class CollectionsScreen : PerformanceCalculatorScreen
    {
        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

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

        private VerboseLoadingLayer loadingLayer;
        private FillFlowContainer collectionsViewContainer;
        private GridContainer collectionContainer;
        private SpriteText collectionNameText;
        private FillFlowContainer<DrawableExtendedProfileScore> drawableScores;

        private CancellationTokenSource calculationCancellatonToken;
        private Collection currentCollection;
        private NotifyCollectionChangedEventHandler collectionChangedEventHandler;

        private const float collection_controls_height = 40;

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
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        collectionNameText = new SpriteText
                                        {
                                            Margin = new MarginPadding { Left = 8 },
                                            Font = new FontUsage(size: 28),
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft
                                        },
                                        new EmptyDrawable(),
                                        new StatefulButton("Overwrite pp values")
                                        {
                                            Width = 150,
                                            Height = collection_controls_height,
                                            Action = () =>
                                            {
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

            if (RuntimeInfo.IsDesktop)
                HotReloadCallbackReceiver.CompilationFinished += _ => performCalculation();

            ruleset.ValueChanged += _ => performCalculation();
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
            if (currentCollection != null)
                currentCollection.Scores.CollectionChanged -= collectionChangedEventHandler;

            currentCollection = collection;
            collectionNameText.Text = collection.Name.Value;

            // Store the event handler to unsubscribe when opening a different collection
            collectionChangedEventHandler = (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                    performCalculation(e.NewItems.Cast<ScoreInfo>(), false);
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                    drawableScores.RemoveAll(x => e.OldItems.Cast<long>().Contains(x.Score.SoloScore.OnlineID), true);
            };

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

                    var livePP = score.PP ?? 0.0;
                    var perfAttributes = await performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, calculationCancellatonToken.Token)!;

                    if (overwritePp) score.PP = perfAttributes?.Total ?? 0.0;

                    addScoreToUI(new ExtendedProfileScore(score, livePP, difficultyAttributes, perfAttributes));
                }

                DrawableExtendedProfileScore[] sortedScores = drawableScores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total - ((ExtendedProfileScore)x.Score).LivePP).ToArray();

                for (int i = 0; i < sortedScores.Length; i++)
                {
                    drawableScores.SetLayoutPosition(sortedScores[i], i);
                }

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

                drawableScores.Add(drawable);
            });
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
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = null;
        }

        private partial class EmptyDrawable : Drawable
        {

        }
    }
}
