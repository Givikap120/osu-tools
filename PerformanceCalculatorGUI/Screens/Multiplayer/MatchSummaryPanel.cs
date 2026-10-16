// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using PerformanceCalculatorGUI.Components;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class MatchSummaryPanel : CompositeDrawable
    {
        private readonly RoomHistory history;
        private IReadOnlyList<RoomGame> games;
        private FillFlowContainer results = null!;
        private OsuDropdown<ScoreType> dropdown = null!;
        private StatefulButton calculateButton = null!;
        private OsuSpriteText status = null!;
        private OverlayColourProvider colours = null!;
        private GameHost host = null!;

        public MatchSummaryPanel(RoomHistory history, IReadOnlyList<RoomGame> games)
        {
            this.history = history;
            this.games = games;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, GameHost host)
        {
            this.colours = colours;
            this.host = host;

            FillFlowContainer content;
            GridContainer controls;
            OsuSpriteText title;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Masking = true, CornerRadius = 8,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 }
                },
                content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical, Padding = new MarginPadding(14),
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        title = label("Match Summary", 25),
                        controls = new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Depth = -1,
                            ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.AutoSize) },
                            Content = new[] { new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Right = 10 },
                                    Child = dropdown = new OsuDropdown<ScoreType>
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Items = new[] { ScoreType.None, ScoreType.Lazer, ScoreType.ScoreV2 },
                                        Current = { Value = ScoreType.None }
                                    }
                                },
                                calculateButton = new StatefulButton("Calculate")
                                {
                                    Width = 100,
                                }
                            } }
                        },
                        results = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical, Spacing = new Vector2(0, 5)
                        }
                    }
                }
            };

            renderSummary();
        }

        private void renderSummary()
        {
            results.Clear();
            var summary = MatchSummary.Calculate(games);
            if (summary.Players.Count == 0)
            {
                results.Add(label("No completed games with scores.", 18));
                return;
            }

            if (summary.TeamMaps > 0)
            {
                string winner = summary.RedPoints == summary.BluePoints ? "Draw"
                    : summary.RedPoints > summary.BluePoints ? "Red team wins" : "Blue team wins";

                results.Add(label($"Red {summary.RedPoints} – {summary.BluePoints} Blue", 22));
                results.Add(label(winner, 22));
            }
            else
            {
                bool duel = summary.Players.Count == 2;
                var first = summary.Players[0];

                string result = duel
                    ? summary.Players[0].Wins == summary.Players[1].Wins ? "Draw" : $"{history.Username(first.UserId)} wins on points"
                    : $"{history.Username(first.UserId)} leads by average z-score";

                if (duel)
                    result += $" · {first.Wins} – {summary.Players[1].Wins}";

                results.Add(label(result, 22));
            }

            bool showWins = summary.SoloMaps > 0;

            results.Add(row("PLAYER", "MAPS", showWins ? "WINS" : "", "AVG SCORE", "AVG Z", null, true));

            foreach (var player in summary.Players)
            {
                string? team = player.Teams.Count == 1 ? player.Teams.Single() : null;
                Colour4? teamColour = team != null ? RoomGameCard.TeamColour(team) : null;

                Enum.TryParse<CountryCode>(history.Users.GetValueOrDefault(player.UserId)?.CountryCode, true, out var country);

                var identity = new OsuClickableContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.Both,
                    Action = () => host.OpenUrlExternally($"https://osu.ppy.sh/users/{player.UserId}"),
                    Children = new Drawable[]
                    {
                        new DrawableFlag(country)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(26, 18),
                            FillMode = FillMode.Fit
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 34, Right = 8 },
                            Child = label(history.Username(player.UserId), 16)
                        }
                    }
                };

                results.Add(row(identity, player.Played.ToString(), showWins ? player.Wins.ToString() : "",
                    player.AverageScore.ToString("N0"), player.AverageZ.ToString("+0.00;-0.00;0.00"), teamColour));
            }
        }

        private static OsuSpriteText rowLabel(string text)
        {
            OsuSpriteText result = label(text, 16);
            result.Origin = Anchor.CentreLeft;
            result.Anchor = Anchor.CentreLeft;
            return result;
        }

        private Drawable row(string player, string played, string wins, string average, string z, Colour4? team, bool heading = false)
            => row(rowLabel(player), played, wins, average, z, team, heading);

        private Drawable row(Drawable player, string played, string wins, string average, string z, Colour4? team, bool heading = false)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = heading ? 28 : 44,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = heading ?
                        colours.Background4 :
                        colours.Background3
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                        Colour = team ?? Colour4.Transparent
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 12, Right = 12, Top = 5, Bottom = 5 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(), new Dimension(GridSizeMode.Relative, 0.10f),
                            new Dimension(GridSizeMode.Relative, 0.10f), new Dimension(GridSizeMode.Relative, 0.22f),
                            new Dimension(GridSizeMode.Relative, 0.14f)
                        },
                        Content = new[] { new[] { player, rowLabel(played), rowLabel(wins), rowLabel(average), rowLabel(z) } }
                    }
                }
            };
        }

        private static OsuSpriteText label(string text, float size) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = text,
            Font = OsuFont.GetFont(size: size),
        };
    }
}

