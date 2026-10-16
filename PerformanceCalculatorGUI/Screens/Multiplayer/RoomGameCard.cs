// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private readonly RoomGame game;
        private const float corner_radius = 8;

        public RoomGameCard(RoomHistory history, RoomGame game)
        {
            this.history = history;
            this.game = game;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = corner_radius;
        }

        private readonly Dictionary<string, List<Drawable>> columnContents = new();

        private Drawable alignedColumn(string key, Drawable content, Anchor anchor = Anchor.BottomLeft, Anchor contentAnchor = Anchor.CentreRight)
        {
            if (!columnContents.TryGetValue(key, out var group))
                columnContents[key] = group = new List<Drawable>();

            group.Add(content);

            content.Anchor = contentAnchor;
            content.Origin = contentAnchor;

            var cell = new Container
            {
                Anchor = anchor,
                Origin = anchor,
                Child = content
            };

            cell.OnUpdate += _ => cell.Size = new Vector2(
                group.Max(d => d.Width),
                group.Max(d => d.Height));

            return cell;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, LargeTextureStore textures, GameHost host)
        {
            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8)
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

            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Left = 8, Right = 8, Bottom = 8 },
                Spacing = new Vector2(0, 5)
            };
            content.Add(rows);
            var ruleset = RulesetHelper.GetRulesetFromLegacyID(game.RulesetId);

            if (game.HasTeams)
            {
                string[] teams = game.TeamTotal("blue") > game.TeamTotal("red")
                    ? new[] { "blue", "red" } : new[] { "red", "blue" };

                foreach (string team in teams)
                    foreach (var score in game.Scores.Where(s => game.TeamFor(s) == team).OrderByDescending(s => s.TotalScore))
                        rows.Add(new RoomScoreCard(history, game, score, columnContents));

                foreach (var score in game.Scores.Where(s => game.TeamFor(s) is not ("red" or "blue")).OrderByDescending(s => s.TotalScore))
                    rows.Add(new RoomScoreCard(history, game, score, columnContents));

                content.Add(createTeamFooter(colours));
            }
            else
            {
                foreach (var score in game.Scores.OrderByDescending(s => s.TotalScore))
                    rows.Add(new RoomScoreCard(history, game, score, columnContents));
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
                header.Add(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get($"https://assets.ppy.sh/beatmaps/{set.Id}/covers/cover.jpg"),
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0.5f
                });

            string mode = game.RulesetId switch { 0 => "osu!", 1 => "osu!taiko", 2 => "osu!catch", 3 => "osu!mania", _ => $"Ruleset {game.RulesetId}" };
            string matchInfo = $"{game.Details.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {mode}";
            string freestyleInfo = game.Freestyle ? " · Freestyle" : string.Empty;
            string matchType = $" · {game.Details.RoomType.Replace('_', ' ')}";
            string beatmapTitle = set == null ? $"Unavailable beatmap ({game.BeatmapId})" : $"{set.Artist} — {set.Title}";
            if (!game.Freestyle && beatmap != null)
                beatmapTitle += $" [{beatmap.Version}]";

            header.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(14),
                Spacing = new Vector2(0, 5),
                Children = new Drawable[] { text(matchInfo + freestyleInfo + matchType, 15), text(beatmapTitle, 23) }
            });
            return header;
        }

        private Drawable createTeamFooter(OverlayColourProvider colours)
        {
            long red = game.TeamTotal("red");
            long blue = game.TeamTotal("blue");
            var footer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 70,
                        Padding = new MarginPadding { Left = 18, Right = 18, Top = 0, Bottom = 0 },
                        Children = new Drawable[] { teamTotal("Red Team", red, "red", Anchor.TopLeft), teamTotal("Blue Team", blue, "blue", Anchor.TopRight) }
                    }
                }
            };
            bool aborted = history.Events.Values.Any(e => e.GameId == game.Id && e.Type == "game_aborted");
            if (game.Expired && !aborted && game.Scores.All(s => game.TeamFor(s) is "red" or "blue"))
            {
                footer.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 38,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.GetFont(size: 17, weight: FontWeight.Bold),
                            Text = red == blue ? "Draw" : $"{(red > blue ? "Red" : "Blue")} Team wins by {Math.Abs(red - blue):N0}"
                        }
                    }
                });
            }
            return footer;
        }

        private static Drawable teamTotal(string name, long total, string team, Anchor anchor) => new FillFlowContainer
        {
            Anchor = anchor,
            Origin = anchor,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 4),
            Children = new Drawable[]
            {
                new OsuSpriteText { Anchor = anchor, Origin = anchor, Text = name, Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold), Colour = TeamColour(team) },
                new OsuSpriteText { Anchor = anchor, Origin = anchor, Text = $"{total:N0}", Font = OsuFont.GetFont(size: 26) }
            }
        };

        private static OsuSpriteText text(string value, float size, Colour4? colour = null) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = value,
            Font = OsuFont.GetFont(size: size),
            Colour = colour ?? Colour4.White
        };

        public static Colour4 TeamColour(string team) => team == "red" ? Colour4.FromHex("FF7799") : Colour4.FromHex("77BBFF");
    }
}
