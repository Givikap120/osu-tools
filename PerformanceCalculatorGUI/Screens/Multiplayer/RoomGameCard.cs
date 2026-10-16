// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class RoomGameCard : Container
    {
        private readonly RoomHistory history;
        private readonly RoomEvent roomEvent;
        private readonly RoomGame game;

        private const float corner_radius = 8;

        public RoomGameCard(RoomHistory history, RoomEvent roomEvent, RoomGame game)
        {
            this.history = history;
            this.roomEvent = roomEvent;
            this.game = game;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = corner_radius;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, LargeTextureStore textures, GameHost host)
        {

            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2)
            };

            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                content
            };

            content.Add(createHeader(colours, textures, host));

            if (game.Scores.Count == 0)
            {
                content.Add(text("No scores were submitted.", 16));
                return;
            }

            if (game.HasTeams)
            {
                string[] teams = game.TeamTotal("blue") > game.TeamTotal("red") ? new[] { "blue", "red" } : new[] { "red", "blue" };
                foreach (string team in teams)
                {
                    content.Add(text($"{team.ToUpperInvariant()} TEAM · {game.TeamTotal(team):N0}", 18, teamColour(team)));
                    foreach (var score in game.Scores.Where(s => game.TeamFor(s) == team).OrderByDescending(s => s.TotalScore))
                        content.Add(scoreRow(score, teamColour(team), host));
                }

                // Missing team metadata should not make submitted scores disappear.
                foreach (var score in game.Scores.Where(s => game.TeamFor(s) is not ("red" or "blue")).OrderByDescending(s => s.TotalScore))
                    content.Add(scoreRow(score, colours.Content2, host));

                if (game.Scores.All(s => game.TeamFor(s) is "red" or "blue"))
                {
                    long red = game.TeamTotal("red");
                    long blue = game.TeamTotal("blue");
                    content.Add(text(red == blue ? "Draw" : $"{(red > blue ? "Red" : "Blue")} team wins by {System.Math.Abs(red - blue):N0}", 18));
                }
            }
            else
            {
                foreach (var score in game.Scores.OrderByDescending(s => s.TotalScore))
                    content.Add(scoreRow(score, colours.Content1, host));
            }
        }

        private OsuClickableContainer createHeader(OverlayColourProvider colours, LargeTextureStore textures, GameHost host)
        {
            history.Beatmaps.TryGetValue(game.BeatmapId, out var beatmap);

            var header = new OsuClickableContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 106,
                Masking = true,
                CornerRadius = corner_radius,
                Action = () => host.OpenUrlExternally($"https://osu.ppy.sh/beatmaps/{game.BeatmapId}")
            };

            header.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background3 });

            var set = beatmap == null ? null : history.BeatmapSets.GetValueOrDefault(beatmap.BeatmapSetId);
            if (set != null)
            {
                header.Add(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get($"https://assets.ppy.sh/beatmaps/{set.Id}/covers/cover.jpg"),
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0.5f
                });
            }

            string mode = game.RulesetId switch { 0 => "osu!", 1 => "osu!taiko", 2 => "osu!catch", 3 => "osu!mania", _ => $"Ruleset {game.RulesetId}" };
            string matchInfo = $"{game.Details.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {mode}";
            string freestyleInfo = game.Freestyle ? " · Freestyle" : string.Empty;
            string matchType = $" · {game.Details.RoomType.Replace('_', ' ')}";

            string beatmapTitle = set == null ? $"Unavailable beatmap ({game.BeatmapId})" : $"{set.Artist} — {set.Title}";
            if (game.Freestyle == false && beatmap != null)
                beatmapTitle += $" [{beatmap.Version}]";

            header.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(14),
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    text(matchInfo + freestyleInfo + matchType, 15),
                    text(beatmapTitle, 23),
                }
            });

            return header;
        }

        private Drawable scoreRow(RoomScore score, Colour4 colour, GameHost host)
        {
            string mods = string.Join(" ", score.Mods.Select(m => (string?)m["acronym"]));
            string stats = string.Join("   ", score.Statistics.Select(s => $"{s.Key.Replace('_', ' ')}: {s.Value:N0}"));
            string difficulty = game.Freestyle ? $" · [{history.Beatmaps.GetValueOrDefault(score.BeatmapId)?.Version ?? score.BeatmapId.ToString()}]" : string.Empty;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 70,
                Padding = new MarginPadding { Left = 14, Right = 14, Top = 6, Bottom = 6 },
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Y, Width = 3, Colour = colour },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 12 },
                        RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, 30), new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Relative, 0.55f) },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new OsuClickableContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Action = () => host.OpenUrlExternally($"https://osu.ppy.sh/users/{score.UserId}"),
                                                Child = text($"{history.Username(score.UserId)}{difficulty}  {mods}", 18, colour)
                                            },
                                            text($"{score.TotalScore:N0}   ·   {score.Accuracy:P2}   ·   {score.MaxCombo:N0}x   ·   {(score.Passed ? score.Rank : "Failed")}", 18)
                                        }
                                    }
                                }
                            },
                            new Drawable[] { text(stats, 14) }
                        }
                    }
                }
            };
        }

        private static OsuSpriteText text(string value, float size, Colour4? colour = null) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = value,
            Font = OsuFont.GetFont(size: size),
            Colour = colour ?? Colour4.White
        };

        private static Colour4 teamColour(string team) => team == "red" ? Colour4.FromHex("FF7799") : Colour4.FromHex("77BBFF");
    }
}
