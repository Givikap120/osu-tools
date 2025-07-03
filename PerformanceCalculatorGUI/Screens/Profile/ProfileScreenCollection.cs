// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Components.Scores;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;
using osu.Framework.Allocation;
using System.Linq;
using osu.Framework.Logging;
using System;
using osu.Framework.Graphics;
using osu.Game.Online.API.Requests.Responses;
using osu.Framework.Graphics.Containers;

namespace PerformanceCalculatorGUI.Screens.Profile
{
    public partial class ProfileScreen
    {
        [Resolved]
        private CollectionManager collections { get; set; }

        private ProfileCollection getCollection(string username)
        {
            var collection = collections.CollectionProfiles.FirstOrDefault(
                c => (c.Player.Value?.IsThisUsername(username) ?? false) && (c.RulesetId == ruleset.Value.OnlineID));
            if (collection == null) return null;

            currentPlayer = collection.Player.Value;

            return collection;
        }

        private void resetPlayerCollectionFromServer(string username)
        {
            var task = calculateProfilesFromServer([username]);

            task.ContinueWith(c =>
            {
                if (currentPlayer == null) return;

                var collection = collections.CollectionProfiles.FirstOrDefault(c => c.Player.Value?.IsThisUsername(username) ?? false);
                if (collection == null)
                {
                    collection = new ProfileCollection(currentPlayer, ruleset.Value.OnlineID);
                    collections.CollectionProfiles.Add(collection);
                }

                collection.Player.Value = currentPlayer;
                collection.BonusPp = playcountBonusPP;

                collection.Scores.Clear();
                var allScores = GetProfileScores();

                foreach (var score in allScores)
                {
                    collection.Scores.Add(score);
                }

                collections.SaveCollectionProfiles();
            });
        }

        private void calculateProfileFromCollection(string username)
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = new CancellationTokenSource();

            scores.Clear();

            var collection = getCollection(username);
            var collectionScores = collection?.Scores ?? Enumerable.Empty<ScoreInfo>();

            if (!collectionScores.Any())
            {
                resetPlayerCollectionFromServer(username);
                return;
            }

            loadingLayer.Show();

            var plays = new List<ExtendedProfileScore>();

            Task.Run(async () =>
            {
                Schedule(() =>
                {
                    if (userPanel != null)
                        userPanelContainer.Remove(userPanel, true);

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

                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (ScoreInfo score in collectionScores)
                {
                    if (calculationCancellatonToken == null || calculationCancellatonToken.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapInfo.OnlineID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    Mod[] mods = score.Mods;

                    var parsedScore = new ProcessorScoreDecoder(working).Parse(score);

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(mods);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                    if (calculationCancellatonToken == null || calculationCancellatonToken.IsCancellationRequested)
                        return;

                    double livePP = score.PP ?? 0.0;
                    var perfAttributes = await (performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, calculationCancellatonToken.Token)).ConfigureAwait(false)!;

                    var play = new ExtendedProfileScore(score, livePP, difficultyAttributes, perfAttributes);
                    plays.Add(play);
                    addScoreToUI(play, true);
                }

                var localOrdered = plays.OrderByDescending(x => x.PerformanceAttributes.Total).ToList();
                var liveOrdered = plays.OrderByDescending(x => x.LivePP ?? 0).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        if (play.LivePP != null)
                        {
                            play.Position.Value = localOrdered.IndexOf(play) + 1;
                            play.PositionChange.Value = liveOrdered.IndexOf(play) - localOrdered.IndexOf(play);
                        }
                    }

                    updateSorting(sorting.Value);
                });

                decimal totalLocalPP = 0;
                for (int i = 0; i < localOrdered.Count; i++)
                    totalLocalPP += (decimal)(Math.Pow(0.95, i) * localOrdered[i].PerformanceAttributes.Total);

                decimal totalLivePP = 0;
                for (int i = 0; i < liveOrdered.Count; i++)
                    totalLivePP += (decimal)(Math.Pow(0.95, i) * (liveOrdered[i].LivePP ?? 0));

                var player = await apiManager.GetJsonFromApi<APIUser>($"users/{username}/{ruleset.Value.ShortName}").ConfigureAwait(false);

                Schedule(() =>
                {
                    userPanelContainer.Add(userPanel = new UserCard(player)
                    {
                        RelativeSizeAxes = Axes.X
                    });

                    userPanel.Data.Value = new UserCardData
                    {
                        LivePP = totalLivePP + collection.BonusPp,
                        LocalPP = totalLocalPP + collection.BonusPp,
                        PlaycountPP = collection.BonusPp,
                    };
                });

            }, calculationCancellatonToken.Token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                    calculationButton.State.Value = ButtonState.Done;
                    updateSorting(ProfileSortCriteria.Local);
                    isCalculating = false;
                });
            }, TaskContinuationOptions.None);
        }
    }
}
