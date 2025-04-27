// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;
using osu.Game.Scoring.Legacy;
using System.IO;
using osuTK.Graphics;
using osu.Framework.Logging;
using PerformanceCalculatorGUI.Components.Scores;
using PerformanceCalculatorGUI.Screens.Profile;
using PerformanceCalculatorGUI.Utils;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ProfileScreen
    {
        // For now it supports only one user, maybe in future I will change this
        private RecalculationUser currentUser;

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
                    currentUser = new RecalculationUser(player.Username, player.Id, player.PreviousUsernames);
                }
                catch (Exception)
                {
                    currentUser = new RecalculationUser(username);
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

                        tempScores.Add(new ProfileScore(score, difficultyAttributes, perfAttributes));
                    }

                    var topScore = tempScores.MaxBy(s => s.SoloScore.PP);
                    if (topScore == null)
                        continue;

                    plays.Add(topScore);
                    Schedule(() => scores.Add(new DrawableProfileScore(topScore)
                    {
                        PopoverMaker = () => new ProfileScreenScorePopover(topScore, this)
                    }));
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
                    isCalculating = false;
                });
            }, token);
        }

        private List<List<ScoreInfo>> getRelevantScores(ScoreInfoCacheManager scoreManager)
        {
            Schedule(() => loadingLayer.Text.Value = "Getting user scores...");
            var realmScores = scoreManager.GetScores();

            Schedule(() => loadingLayer.Text.Value = "Filtering scores...");

            realmScores.RemoveAll(x => !currentUser.IsThisUsername(x.User.Username) // Wrong username
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
    }
}
