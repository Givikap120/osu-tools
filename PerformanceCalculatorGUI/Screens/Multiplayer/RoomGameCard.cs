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
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class RoomGameCard : Container
    {
        private readonly RoomHistory history;
        private readonly RoomGame game;
        private const float corner_radius = 8;
        private static readonly Colour4 scoreColour = Colour4.FromHex("FF66AB");

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
                        rows.Add(scoreRow(score, teamColour(team), colours, ruleset, host));

                foreach (var score in game.Scores.Where(s => game.TeamFor(s) is not ("red" or "blue")).OrderByDescending(s => s.TotalScore))
                    rows.Add(scoreRow(score, null, colours, ruleset, host));

                content.Add(createTeamFooter(colours));
            }
            else
            {
                foreach (var score in game.Scores.OrderByDescending(s => s.TotalScore))
                    rows.Add(scoreRow(score, null, colours, ruleset, host));
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

        private Drawable scoreRow(RoomScore score, Colour4? team, OverlayColourProvider colours, Ruleset ruleset, GameHost host)
        {
            string difficulty = history.Beatmaps.GetValueOrDefault(score.BeatmapId)?.Version ?? score.BeatmapId.ToString();

            Enum.TryParse<CountryCode>(history.Users.GetValueOrDefault(score.UserId)?.CountryCode, true, out var country);
            if (!Enum.TryParse<ScoreRank>(score.Rank, true, out var rank) || !score.Passed)
                rank = ScoreRank.F;

            var scoreContent = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
                Children = new Drawable[]
                {
                    alignedColumn("mods", new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        Children = score.Mods.Select(m => new ModIcon(m.ToObject<APIMod>()!.ToMod(ruleset))
                        {
                            Scale = new Vector2(0.32f)
                        }).ToArray()
                    }, Anchor.CentreLeft, Anchor.CentreLeft),
                    scoreDetails(score, colours)
                }
            };

            const float freestyle_padding = -8;

            var player = new OsuClickableContainer
            {
                RelativeSizeAxes = Axes.Both,
                Action = () => host.OpenUrlExternally($"https://osu.ppy.sh/users/{score.UserId}"),
                Children = new Drawable[]
                {
                    new DrawableFlag(country)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(26, 18),
                        Y = game.Freestyle ? freestyle_padding : 0,
                        FillMode = FillMode.Fit
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 34, Right = 8 },
                        Child = text(history.Username(score.UserId), 16, colours.Content1).With(t =>
                        {
                            t.Anchor = Anchor.CentreLeft;
                            t.Origin = Anchor.CentreLeft;
                            t.Y = game.Freestyle ? freestyle_padding : 0;
                            t.Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold);
                        })
                    }
                }
            };

            if (game.Freestyle)
            {
                player.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 14,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                    new SpriteIcon
                    {
                        Icon = FontAwesome.Regular.DotCircle,
                        Size = new Vector2(11),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = colours.Content2
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 17, Right = 8 },
                        Child = text(difficulty, 13, colours.Content2).With(t =>
                        {
                            t.Anchor = Anchor.CentreLeft;
                            t.Origin = Anchor.CentreLeft;
                        })
                    }
                    }
                });
            }

            const float rank_width = 44;
            const float rank_gap = 10;

            var right = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Right = rank_width + rank_gap },
                        Child = scoreContent
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(rank_width, 22),
                        Child = new DrawableRank(rank)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre
                        }
                    }
                }
            };

            var layout = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Left = 18, Right = 12, Top = 8, Bottom = 8 },
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize)
                },
                Content = new[] { new Drawable[] { player, right } }
            };

            layout.OnUpdate += _ =>
            {
                float available = Math.Max(0, layout.ChildSize.X);
                float playerSpace = Math.Min(160, available * 0.35f);
                float maxRightWidth = Math.Max(rank_width + rank_gap, available - playerSpace);

                // Natural dimensions do not include Scale.
                float naturalWidth = scoreContent.Width;
                float naturalHeight = scoreContent.Height;

                float scaleX = naturalWidth > 0
                    ? Math.Max(0, maxRightWidth - rank_width - rank_gap) / naturalWidth
                    : 1;

                float scaleY = naturalHeight > 0
                    ? Math.Max(0, layout.ChildSize.Y) / naturalHeight
                    : 1;

                float scale = Math.Min(1, Math.Min(scaleX, scaleY));

                scoreContent.Scale = new Vector2(scale);
                right.Width = naturalWidth * scale + rank_width + rank_gap;
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background3 },
                    team != null ? new Box { RelativeSizeAxes = Axes.Y, Width = 5, Colour = (Colour4)team } : Empty(),
                    layout
                }
            };
        }

        private Drawable scoreDetails(RoomScore score, OverlayColourProvider colours)
        {
            string[] standard = game.RulesetId switch
            {
                0 => new[] { "great", "ok", "meh", "miss" },
                1 => new[] { "great", "ok", "miss" },
                2 => new[] { "great", "large_tick_hit", "small_tick_hit", "small_tick_miss", "miss" },
                3 => new[] { "perfect", "great", "good", "ok", "meh", "miss" },
                _ => Array.Empty<string>()
            };

            var keys = standard.Concat(game.Scores.SelectMany(s => s.Statistics.Keys)
                .Except(standard)
                .OrderBy(key => key, StringComparer.Ordinal))
                .Where(key => key is not ("ignore_hit" or "ignore_miss" or "large_bonus" or "small_bonus"))
                .ToArray();

            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(16, 0),
                    Children = new Drawable[]
                    {
                        metric("COMBO", $"{score.MaxCombo:N0}x", 14, colours.Content1, colours.Content2),
                        metric("ACCURACY", $"{score.Accuracy:P2}", 14, colours.Content1, colours.Content2),
                        metric("SCORE", $"{score.TotalScore:N0}", 24, scoreColour, colours.Content2)
                    }
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = keys.Select(key => metric(
                        key.Replace('_', ' ').ToUpperInvariant(),
                        $"{score.Statistics.GetValueOrDefault(key):N0}",
                        11, colours.Content1, colours.Content2)).ToArray()
                }
            }
            };
        }

        private Drawable metric(string label, string value, float size, Colour4 colour, Colour4 labelColour) => alignedColumn(label, new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(3, 0),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 9),
                    Colour = labelColour,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft
                },
                new OsuSpriteText
                {
                    Text = value,
                    Font = OsuFont.GetFont(size: size, weight: FontWeight.Bold),
                    Colour = colour,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft
                }
            }
        });

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
                new OsuSpriteText { Anchor = anchor, Origin = anchor, Text = name, Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold), Colour = teamColour(team) },
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

        private static Colour4 teamColour(string team) => team == "red" ? Colour4.FromHex("FF7799") : Colour4.FromHex("77BBFF");
    }
}
