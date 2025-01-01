// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK.Graphics;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;
using System.IO;
using osu.Framework.Platform;
using ButtonState = PerformanceCalculatorGUI.Components.ButtonState;
using System.Text;
using osu.Game.Utils;
using osu.Game.Scoring.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Framework.Lists;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ProfileScreen : PerformanceCalculatorScreen
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private VerboseLoadingLayer loadingLayer;

        private GridContainer layout;

        private FillFlowContainer<DrawableProfileScore> scores;

        private LabelledTextBox usernameTextBox;
        private Container userPanelContainer;
        private UserCard userPanel;

        private GridContainer setupContainer;
        private SwitchButton profileImportTypeSwitch;

        private StatefulButton calculationButtonServer;

        private GridContainer localCalcSetupContainer;
        private StatefulButton calculationButtonLocal;
        private RealmSettingsMenu settingsMenu;

        private string[] currentUser;

        private CancellationTokenSource calculationCancellatonToken;

        private OverlaySortTabControl<ProfileSortCriteria> sortingTabControl;
        private readonly Bindable<ProfileSortCriteria> sorting = new Bindable<ProfileSortCriteria>(ProfileSortCriteria.Local);

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private GameHost gameHost { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        private const float setup_width = 220;
        private const float username_container_height = 40;

        public ProfileScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            calculationButtonServer = new StatefulButton("Calculate from server")
            {
                Width = setup_width,
                Height = username_container_height,
                Action = () => { calculateProfileFromServer(usernameTextBox.Current.Value); }
            };

            localCalcSetupContainer = new GridContainer
            {
                Width = setup_width,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize)
                },
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize)
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        calculationButtonLocal = new StatefulButton("Calculate from lazer")
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = username_container_height,
                            Action = () => { calculateProfileFromLazer(usernameTextBox.Current.Value); }
                        },

                        settingsMenu = new RealmSettingsMenu()
                    }
                }
            };
            

            InternalChildren = new Drawable[]
            {
                layout = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension()
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            setupContainer = new GridContainer
                            {
                                Name = "Setup",
                                Height = username_container_height,
                                RelativeSizeAxes = Axes.X,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        usernameTextBox = new ExtendedLabelledTextBox
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Anchor = Anchor.TopLeft,
                                            Label = "Username",
                                            PlaceholderText = "peppy",
                                            CommitOnFocusLoss = false
                                        },
                                        profileImportTypeSwitch = new SwitchButton
                                        {
                                            Width = 80,
                                            Height = username_container_height
                                        },
                                        calculationButtonServer
                                    }
                                }
                            },
                        },
                        new Drawable[]
                        {
                            userPanelContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y
                            }
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    sortingTabControl = new OverlaySortTabControl<ProfileSortCriteria>
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Margin = new MarginPadding { Right = 22 },
                                        Current = { BindTarget = sorting },
                                        Alpha = 0
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer(Direction.Vertical)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = scores = new FillFlowContainer<DrawableProfileScore>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical
                                }
                            }
                        },
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            usernameTextBox.OnCommit += (_, _) => { calculateProfile(usernameTextBox.Current.Value); };
            sorting.ValueChanged += e => { updateSorting(e.NewValue); };

            profileImportTypeSwitch.Current.BindValueChanged(val =>
            {
                calculationCancellatonToken?.Cancel();

                if (val.NewValue)
                {
                    setupContainer.ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize)
                    };
                    setupContainer.Content = new[]
                    {
                        new Drawable[]
                        {
                            usernameTextBox,
                            profileImportTypeSwitch,
                            localCalcSetupContainer,
                        }
                    };
                }
                else
                {
                    setupContainer.ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize)
                    };
                    setupContainer.Content = new[]
                    {
                        new Drawable[]
                        {
                            usernameTextBox,
                            profileImportTypeSwitch,
                            calculationButtonServer
                        }
                    };
                }
            });

            usernameTextBox.OnCommit += (_, _) => { calculateProfile(usernameTextBox.Current.Value); };
        }

        private void calculateProfile(string username)
        {
            if (profileImportTypeSwitch.Current.Value)
                calculateProfileFromLazer(username);
            else
                calculateProfileFromServer(username);
        }

        private void calculateProfileFromServer(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                usernameTextBox.FlashColour(Color4.Red, 1);
                return;
            }

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            calculationButtonServer.State.Value = ButtonState.Loading;

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            Task.Run(async () =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting user data...");

                var player = await apiManager.GetJsonFromApi<APIUser>($"users/{username}/{ruleset.Value.ShortName}");

                currentUser = [player.Username];

                Schedule(() =>
                {
                    if (userPanel != null)
                        userPanelContainer.Remove(userPanel, true);

                    userPanelContainer.Add(userPanel = new UserCard(player)
                    {
                        RelativeSizeAxes = Axes.X
                    });

                    sortingTabControl.Alpha = 1.0f;
                    sortingTabControl.Current.Value = ProfileSortCriteria.Local;

                    layout.RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension()
                    };
                });

                if (token.IsCancellationRequested)
                    return;

                var plays = new List<ExtendedProfileScore>();

                var rulesetInstance = ruleset.Value.CreateInstance();

                Schedule(() => loadingLayer.Text.Value = $"Calculating {player.Username} top scores...");

                var apiScores = await apiManager.GetJsonFromApi<List<SoloScoreInfo>>($"users/{player.OnlineID}/scores/best?mode={ruleset.Value.ShortName}&limit=100");

                foreach (var score in apiScores)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    Mod[] mods = score.Mods.Select(x => x.ToMod(rulesetInstance)).ToArray();

                    var scoreInfo = score.ToScoreInfo(rulesets, working.BeatmapInfo);

                    var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(RulesetHelper.ConvertToLegacyDifficultyAdjustmentMods(rulesetInstance, mods));
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                    var livePp = score.PP ?? 0.0;
                    var perfAttributes = await performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, token)!;
                    score.PP = perfAttributes?.Total ?? 0.0;

                    var extendedScore = new ExtendedProfileScore(score, livePp, perfAttributes);
                    plays.Add(extendedScore);

                    Schedule(() => scores.Add(new DrawableExtendedProfileScore(extendedScore)));
                }

                if (token.IsCancellationRequested)
                    return;

                var localOrdered = plays.OrderByDescending(x => x.SoloScore.PP).ToList();
                var liveOrdered = plays.OrderByDescending(x => x.LivePP).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        play.Position.Value = localOrdered.IndexOf(play) + 1;
                        play.PositionChange.Value = liveOrdered.IndexOf(play) - localOrdered.IndexOf(play);
                        scores.SetLayoutPosition(scores[liveOrdered.IndexOf(play)], localOrdered.IndexOf(play));
                    }
                });

                decimal totalLocalPP = 0;
                for (var i = 0; i < localOrdered.Count; i++)
                    totalLocalPP += (decimal)(Math.Pow(0.95, i) * (localOrdered[i].SoloScore.PP ?? 0));

                decimal totalLivePP = player.Statistics.PP ?? (decimal)0.0;

                decimal nonBonusLivePP = 0;
                for (var i = 0; i < liveOrdered.Count; i++)
                    nonBonusLivePP += (decimal)(Math.Pow(0.95, i) * liveOrdered[i].LivePP);

                //todo: implement properly. this is pretty damn wrong.
                var playcountBonusPP = (totalLivePP - nonBonusLivePP);
                totalLocalPP += playcountBonusPP;

                Schedule(() =>
                {
                    userPanel.Data.Value = new UserCardData
                    {
                        LivePP = totalLivePP,
                        LocalPP = totalLocalPP,
                        PlaycountPP = playcountBonusPP
                    };
                });
            }, token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                    calculationButtonServer.State.Value = ButtonState.Done;
                });
            }, TaskContinuationOptions.None);
        }

        private void calculateProfileFromLazer(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                usernameTextBox.FlashColour(Color4.Red, 1);
                return;
            }

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            calculationButtonLocal.State.Value = ButtonState.Loading;

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            var lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;

            if (lazerPath == string.Empty)
            {
                notificationDisplay.Display(new Notification("Please set-up path to lazer database folder in GUI settings"));
                return;
            }

            var scoreManager = new ScoreInfoCacheManager(gameHost, lazerPath);

            Task.Run(async () =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting user data...");

                APIUser player;
                try
                {
                    player = await apiManager.GetJsonFromApi<APIUser>($"users/{username}/{ruleset.Value.ShortName}");
                    currentUser = [player.Username, .. player.PreviousUsernames, player.Id.ToString()];
                }
                catch (Exception)
                {
                    currentUser = [username];
                    player = new APIUser
                    {
                        Username = username
                    };
                }

                Schedule(() =>
                {
                    if (userPanel != null)
                        userPanelContainer.Remove(userPanel, true);

                    userPanelContainer.Add(userPanel = new UserCard(player)
                    {
                        RelativeSizeAxes = Axes.X
                    });

                    layout.RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension()
                    };
                });

                if (token.IsCancellationRequested)
                    return;

                var plays = new List<ProfileScore>();

                var rulesetInstance = ruleset.Value.CreateInstance();
                var realmScores = getRelevantScores(scoreManager);

                int currentScoresCount = 0;
                var totalScoresCount = realmScores.Sum(childList => childList.Count);

                var allScores = new List<(ScoreInfo score, WorkingBeatmap beatmap, DifficultyAttributes attributes)>();

                var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                foreach (var scoreList in realmScores)
                {
                    string beatmapHash = scoreList[0].BeatmapHash;
                    //get the .osu file from lazer file storage

                    WorkingBeatmap working;
                    try
                    {
                        working = new FlatWorkingBeatmap(Path.Combine(lazerPath, "files", beatmapHash[..1], beatmapHash[..2], beatmapHash));
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    

                    List<ProfileScore> tempScores = [];

                    Dictionary<int, DifficultyAttributes> attributesCache = new();

                    foreach (var score in scoreList)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        Schedule(() => loadingLayer.Text.Value = $"Calculating {player.Username}'s scores... {currentScoresCount} / {totalScoresCount}");

                        DifficultyAttributes difficultyAttributes;
                        int modsHash = RulesetHelper.GenerateModsHash(score.Mods, working.BeatmapInfo.Difficulty, ruleset.Value);

                        if (attributesCache.ContainsKey(modsHash))
                        {
                            difficultyAttributes = attributesCache[modsHash];
                        }
                        else
                        {
                            difficultyAttributes = difficultyCalculator.Calculate(score.Mods);
                            attributesCache[modsHash] = difficultyAttributes;
                        }

                        performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
                        var perfAttributes = await performanceCalculator?.CalculateAsync(score, difficultyAttributes, token)!;

                        score.PP = perfAttributes?.Total ?? 0.0;

                        currentScoresCount++;

                        // Check if passed
                        int totalHitsMap = working.Beatmap.HitObjects.Count;
                        int totalHitsScore = (score.GetCount300() ?? 0) + (score.GetCount100() ?? 0) + (score.GetCount50() ?? 0) + (score.GetCountMiss() ?? 0);
                        if (totalHitsScore < totalHitsMap)
                            continue;

                        // Sanity check for aspire maps till my slider fix won't get merged
                        if (difficultyAttributes.StarRating > 14 && score.BeatmapInfo.Status != BeatmapOnlineStatus.Ranked)
                            continue;

                        if (settingsMenu.ExportInCSV)
                            allScores.Add((score, working, difficultyAttributes));

                        tempScores.Add(new ProfileScore(score, perfAttributes));
                    }

                    var topScore = tempScores.MaxBy(s => s.SoloScore.PP);
                    if (topScore == null)
                        continue;

                    plays.Add(topScore);
                    Schedule(() => scores.Add(new DrawableProfileScore(topScore)));
                }

                if (token.IsCancellationRequested)
                    return;

                var localOrdered = plays.OrderByDescending(x => x.SoloScore.PP).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        play.Position.Value = localOrdered.IndexOf(play) + 1;
                        scores.SetLayoutPosition(scores[plays.IndexOf(play)], localOrdered.IndexOf(play));
                    }
                });

                decimal totalLocalPP = 0;
                for (var i = 0; i < localOrdered.Count; i++)
                    totalLocalPP += (decimal)(Math.Pow(0.95, i) * (localOrdered[i].SoloScore.PP ?? 0));

                decimal totalLivePP = player?.Statistics.PP ?? (decimal)0.0;

                //Calculate bonusPP based of unique score count on ranked diffs
                var playcountBonusPP = (decimal)((417.0 - 1.0 / 3.0) * (1 - Math.Pow(0.995, Math.Min(plays.Count, 1000))));
                totalLocalPP += playcountBonusPP;

                if (settingsMenu.ExportInCSV)
                {
                    Schedule(() => loadingLayer.Text.Value = $"Exporting to csv...");
                    HistoricalScoreExporter.OutputHistoricalScoresCSV(allScores, rulesetInstance, username);
                }

                Schedule(() =>
                {
                    userPanel.Data.Value = new UserCardData
                    {
                        LivePP = totalLivePP,
                        LocalPP = totalLocalPP,
                        PlaycountPP = playcountBonusPP
                    };
                });
            }, token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                    calculationButtonLocal.State.Value = ButtonState.Done;
                });
            }, token);
        }

        private List<List<ScoreInfo>> getRelevantScores(ScoreInfoCacheManager scoreManager)
        {
            Schedule(() => loadingLayer.Text.Value = "Getting user scores...");
            var realmScores = scoreManager.GetScores();

            Schedule(() => loadingLayer.Text.Value = "Filtering scores...");

            realmScores.RemoveAll(x => (!currentUser.Contains(x.User.Username) && !currentUser[0].Equals(x.User.Username, StringComparison.OrdinalIgnoreCase)) // Wrong username
                                    || x.BeatmapInfo == null // No map for score
                                    || x.Passed == false || x.Rank == ScoreRank.F // Failed score
                                    || x.Ruleset.OnlineID != ruleset.Value.OnlineID // Incorrect ruleset
                                    || settingsMenu.ShouldBeFiltered(x) // Customisable filters
                                    );

            List<List<ScoreInfo>> splitScores = realmScores.GroupBy(g => g.BeatmapHash)
                                                            .Select(s => s.ToList())
                                                            .ToList();
            // Simulate scorev1 if enabled
            if (settingsMenu.IsScorev1ningEnabled)
            {
                var rulesetInstance = ruleset.Value.CreateInstance();

                List<List<ScoreInfo>> filteredScores = new();

                foreach (var mapScores in splitScores)
                {
                    List<ScoreInfo> filteredMapScores = mapScores.Where(s => s.IsLegacyScore)
                                                            .GroupBy(x => rulesetInstance.ConvertToLegacyMods(x.Mods))
                                                            .Select(x => x.MaxBy(x => x.LegacyTotalScore))
                                                            .ToList();
                    filteredMapScores.AddRange(mapScores.Where(s => !s.IsLegacyScore));
                    filteredScores.Add(filteredMapScores);
                }

                splitScores = filteredScores;
            }

            return splitScores;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = null;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && !calculationCancellatonToken.IsCancellationRequested)
            {
                calculationCancellatonToken?.Cancel();
            }

            return base.OnKeyDown(e);
        }

        private void updateSorting(ProfileSortCriteria sortCriteria)
        {
            if (!scores.Children.Any())
                return;

            if (profileImportTypeSwitch.Current.Value)
                return;

            DrawableProfileScore[] sortedScores;

            switch (sortCriteria)
            {
                case ProfileSortCriteria.Live:
                    sortedScores = scores.Children.OrderByDescending(x => ((ExtendedProfileScore)x.Score).LivePP).ToArray();
                    break;

                case ProfileSortCriteria.Local:
                    sortedScores = scores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total).ToArray();
                    break;

                case ProfileSortCriteria.Difference:
                    sortedScores = scores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total - ((ExtendedProfileScore)x.Score).LivePP).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortCriteria), sortCriteria, null);
            }

            for (int i = 0; i < sortedScores.Length; i++)
            {
                scores.SetLayoutPosition(sortedScores[i], i);
            }
        }
    }
}
