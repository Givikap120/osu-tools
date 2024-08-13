// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;
using osuTK.Graphics;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class RealmScreen : PerformanceCalculatorScreen
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private StatefulButton calculationButton;
        private VerboseLoadingLayer loadingLayer;

        private GridContainer layout;

        private FillFlowContainer scores;

        private LabelledTextBox usernameTextBox;
        private Container userPanelContainer;
        private UserCard userPanel;

        private RealmSettingsMenu settingsMenu;

        private string[] currentUser;

        private CancellationTokenSource calculationCancellatonToken;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private GameHost gameHost { get; set; }

        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        private const float username_container_height = 40;

        public RealmScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                layout = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, username_container_height), new Dimension(GridSizeMode.Absolute), new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                Name = "Settings",
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
                                        calculationButton = new StatefulButton("Start calculation")
                                        {
                                            Width = 150,
                                            Height = username_container_height,
                                            Action = () => { calculateProfile(usernameTextBox.Current.Value); }
                                        },
                                        settingsMenu = new RealmSettingsMenu()
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
                            new OsuScrollContainer(Direction.Vertical)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = scores = new FillFlowContainer
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
        }

        private void calculateProfile(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                usernameTextBox.FlashColour(Color4.Red, 1);
                return;
            }

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            calculationButton.State.Value = ButtonState.Loading;

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            Task.Run(async () =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting user data...");

                var player = await apiManager.GetJsonFromApi<APIUser>($"users/{username}/{ruleset.Value.ShortName}");

                currentUser = [player.Username, .. player.PreviousUsernames, player.Id.ToString()];

                Schedule(() =>
                {
                    if (userPanel != null)
                        userPanelContainer.Remove(userPanel, true);

                    userPanelContainer.Add(userPanel = new UserCard(player)
                    {
                        RelativeSizeAxes = Axes.X
                    });

                    layout.RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, username_container_height), new Dimension(GridSizeMode.AutoSize), new Dimension() };
                });

                if (token.IsCancellationRequested)
                    return;

                var plays = new List<ProfileScore>();

                var rulesetInstance = ruleset.Value.CreateInstance();

                var lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;

                if (lazerPath == string.Empty)
                {
                    notificationDisplay.Display(new Notification("Please set-up path to lazer database folder in GUI settings"));
                    return;
                }

                var storage = gameHost.GetStorage(lazerPath);
                var realmAccess = new RealmAccess(storage, @"client.realm");

                var realmScores = getRealmScores(realmAccess);

                int currentScoresCount = 0;
                var totalScoresCount = realmScores.Sum(childList => childList.Count);

                foreach (var scoreList in realmScores)
                {
                    string beatmapHash = scoreList[0].BeatmapHash;
                    //get the .osu file from lazer file storage
                    var working = new FlatWorkingBeatmap(Path.Combine(lazerPath, "files", beatmapHash[..1], beatmapHash[..2], beatmapHash));

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

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

                        // Sanity check for aspire maps till my slider fix won't get merged
                        if (difficultyAttributes.StarRating > 14 && score.BeatmapInfo.Status != BeatmapOnlineStatus.Ranked)
                            continue;

                        var perfAttributes = await performanceCalculator?.CalculateAsync(score, difficultyAttributes, token)!;

                        score.PP = perfAttributes?.Total ?? 0.0;

                        tempScores.Add(new ProfileScore(score, perfAttributes));
                        currentScoresCount++;
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

                decimal totalLivePP = player.Statistics.PP ?? (decimal)0.0;

                //Calculate bonusPP based of unique score count on ranked diffs
                var playcountBonusPP = (decimal)((417.0 - 1.0 / 3.0) * (1 - Math.Pow(0.995, Math.Min(realmScores.Count, 1000))));
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
                    calculationButton.State.Value = ButtonState.Done;
                });
            }, token);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = null;
        }

        private List<List<ScoreInfo>> getRealmScores(RealmAccess realm)
        {
            Schedule(() => loadingLayer.Text.Value = "Getting user scores...");
            var realmScores = realm.Run(r => r.All<ScoreInfo>().Detach());

            Schedule(() => loadingLayer.Text.Value = "Filtering scores...");

            realmScores.RemoveAll(x => !currentUser.Contains(x.User.Username) // Wrong username
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
                    filteredScores.Add(mapScores);
                }

                splitScores = filteredScores;
            }

            return splitScores;
        }

    }
}
