// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class MultiplayerScreen : PerformanceCalculatorScreen
    {
        public ScoreType CurrentType { get; private set; } = ScoreType.None;
        public RoomHistory? History { get; private set; }
        public FillFlowContainer<RoomGameCard> GameCards { get; private set; } = null!;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Green);

        [Resolved]
        private APIManager api { get; set; } = null!;

        private ExtendedLabelledTextBox roomInput = null!;
        private StatefulButton loadButton = null!;
        private FillFlowContainer timeline = null!;
        private OsuScrollContainer scroll = null!;
        private MatchSummaryPanel summaryPanel = null!;
        private CancellationTokenSource? loadCancellation;
        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        public MultiplayerScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void ConvertTo(ScoreType type)
        {
            if (CurrentType == type)
                return;

            CurrentType = type;

            foreach (RoomGameCard gameCard in GameCards.Children)
            {
                gameCard.ConvertTo(type);
            }
        }

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
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, 40), new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.AutoSize) },
                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        roomInput = new ExtendedLabelledTextBox
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Label = "Multiplayer room",
                                            PlaceholderText = "Room ID or link",
                                            CommitOnFocusLoss = false,
                                            SelectAllOnFocus = true
                                        },
                                        loadButton = new StatefulButton("Load")
                                        {
                                            Width = 120,
                                            Action = startLoad
                                        }
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            scroll = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = timeline = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding(16),
                                    Spacing = new Vector2(0, 20)
                                }
                            }
                        }
                    }
                }
            };
            roomInput.OnCommit += (_, _) => startLoad();
        }

        private void startLoad()
        {
            cancelLoad();

            if (!tryParseRoomLink(roomInput.Current.Value, out long roomId, out bool stable))
                return;

            CurrentType = ScoreType.None;
            History = null;
            timeline.Clear();
            loadButton.State.Value = ButtonState.Loading;
            loadCancellation = new CancellationTokenSource();
            _ = loadRoomAsync(roomId, stable, loadCancellation.Token);
        }

        private async Task loadRoomAsync(long roomId, bool stable, CancellationToken token)
        {
            try
            {
                var history = await loadHistoryAsync(roomId, stable, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    History = history;
                    renderHistory(history);
                    loadButton.State.Value = ButtonState.Initial;
                    scroll.ScrollToStart();
                });
            }
            catch (Exception)
            {
                Schedule(() =>
                {
                    loadButton.State.Value = ButtonState.Initial;
                });
            }
        }

        private async Task<RoomEventsResponse> getPageFromApi(long roomId, bool stable, (string, string)[]? parameters, CancellationToken cancellationToken)
        {
            if (!stable)
            {
                return await api.GetJsonFromApi<RoomEventsResponse>($"rooms/{roomId}/events", parameters, cancellationToken).ConfigureAwait(false);
            }

            var response = await api.GetJsonFromApi<JObject>(
                    $"matches/{roomId}", parameters, cancellationToken).ConfigureAwait(false);

            return StableRoomConverter.Convert(response);
        }

        private async Task<RoomHistory> loadHistoryAsync(long roomId, bool stable, CancellationToken cancellationToken, Action<int>? progress = null)
        {
            var history = new RoomHistory();
            long? before = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parameters = before == null
                    ? new[] { ("limit", "100") }
                    : new[] { ("limit", "100"), ("before", before.Value.ToString(CultureInfo.InvariantCulture)) };

                var page = await getPageFromApi(roomId, stable, parameters, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                history.Merge(page);
                progress?.Invoke(history.Events.Count);

                if (page.Events.Count == 0)
                    break;

                long oldest = page.Events.Min(e => e.Id);
                if (oldest <= page.FirstEventId)
                    break;
                if (before.HasValue && oldest >= before.Value)
                    throw new InvalidOperationException("Room history pagination did not advance.");

                before = oldest;
            }

            return history;
        }

        private void renderHistory(RoomHistory history)
        {
            timeline.Clear();

            var renderedGames = new HashSet<long>();
            var games = new List<RoomGame>();

            foreach (var entry in history.Events.Values)
            {
                if (entry.GameId == null
                    || entry.Type is not ("game_completed" or "game_started"))
                    continue;

                var game = history.Games.GetValueOrDefault(entry.GameId.Value);

                if (game?.Expired != true || !renderedGames.Add(game.Id))
                    continue;

                games.Add(game);
            }

            if (games.Count == 0)
            {
                timeline.Add(new OsuSpriteText
                {
                    Text = "This room has no completed games."
                });
                return;
            }

            timeline.Add(summaryPanel = new MatchSummaryPanel(this, history, games)
            {
                Depth = -1
            });

            timeline.Add(GameCards = new FillFlowContainer<RoomGameCard>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 20)
            });

            foreach (var game in games)
                GameCards.Add(new RoomGameCard(history, game));

            // Must be called after game cards are added
            summaryPanel.UpdateSummary();
        }

        private void cancelLoad()
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = null;

            if (loadButton != null)
                loadButton.State.Value = ButtonState.Initial;
        }

        private static bool tryParseRoomLink(string input, out long roomId, out bool stable)
        {
            roomId = 0;
            stable = false;
            input = input.Trim();

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
                || uri.Host != "osu.ppy.sh"
                || uri.Scheme is not ("http" or "https"))
                return false;

            string[] parts = uri.AbsolutePath.Trim('/').Split('/');

            stable = parts.Length == 3
                     && parts[0] == "community"
                     && parts[1] == "matches";

            bool lazer = parts.Length is 3 or 4
                         && parts[0] == "multiplayer"
                         && parts[1] == "rooms"
                         && (parts.Length == 3 || parts[3] == "events");

            return (stable || lazer)
                   && long.TryParse(parts[2], NumberStyles.None,
                       CultureInfo.InvariantCulture, out roomId)
                   && roomId > 0;
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            cancelLoad();
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;
            }
            base.Dispose(isDisposing);
        }
    }
}
