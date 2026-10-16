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
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class RoomScoreCard : CompositeDrawable
    {
        public RoomScore Score { get; private set; }
        public string? Team { get; private set; }

        private readonly RoomHistory history;
        private readonly RoomGame game;
        private readonly Dictionary<string, List<Drawable>> columnContents;
        private readonly List<(string Key, Drawable Content)> registeredColumns = new();

        public RoomScoreCard(RoomHistory history, RoomGame game, RoomScore score,
            Dictionary<string, List<Drawable>> columnContents)
        {
            this.history = history;
            this.game = game;
            this.columnContents = columnContents;
            Score = score;
            Team = game.HasTeams && game.TeamFor(score) is ("red" or "blue")
                ? game.TeamFor(score) : null;

            RelativeSizeAxes = Axes.X;
            Height = 50;
            Masking = true;
            CornerRadius = 4;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, GameHost host)
        {
            var ruleset = RulesetHelper.GetRulesetFromLegacyID(Score.RulesetId);
            string difficulty = history.Beatmaps.GetValueOrDefault(Score.BeatmapId)?.Version ?? Score.BeatmapId.ToString();

            Enum.TryParse<CountryCode>(history.Users.GetValueOrDefault(Score.UserId)?.CountryCode, true, out var country);
            if (!Enum.TryParse<ScoreRank>(Score.Rank, true, out var rank) || !Score.Passed)
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
                        Children = Score.Mods.Select(m => new ModIcon(m.ToObject<APIMod>()!.ToMod(ruleset))
                        {
                            Scale = new Vector2(0.32f)
                        }).ToArray()
                    }, Anchor.CentreLeft, Anchor.CentreLeft),
                    scoreDetails(Score, colours)
                }
            };

            const float freestyle_padding = -8;

            var player = new OsuClickableContainer
            {
                RelativeSizeAxes = Axes.Both,
                Action = () => host.OpenUrlExternally($"https://osu.ppy.sh/users/{Score.UserId}"),
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
                        Child = text(history.Username(Score.UserId), 16, colours.Content1).With(t =>
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

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background3 },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 5,
                    Colour = Team == null ? Colour4.White : RoomGameCard.TeamColour(Team),
                    Alpha = Team == null ? 0 : 1
                },
                layout
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
                        metric("SCORE", $"{score.TotalScore:N0}", 24, Colour4.FromHex("FF66AB"), colours.Content2)
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

        private Drawable alignedColumn(string key, Drawable content,
            Anchor anchor = Anchor.BottomLeft, Anchor contentAnchor = Anchor.CentreRight)
        {
            if (!columnContents.TryGetValue(key, out var group))
                columnContents[key] = group = new List<Drawable>();

            group.Add(content);
            registeredColumns.Add((key, content));
            content.Anchor = contentAnchor;
            content.Origin = contentAnchor;

            var cell = new Container
            {
                Anchor = anchor,
                Origin = anchor,
                Child = content
            };
            cell.OnUpdate += _ =>
            {
                if (group.Count > 0)
                    cell.Size = new Vector2(group.Max(d => d.Width), group.Max(d => d.Height));
            };
            return cell;
        }

        private static OsuSpriteText text(string value, float size, Colour4? colour = null) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = value,
            Font = OsuFont.GetFont(size: size),
            Colour = colour ?? Colour4.White
        };

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var (key, content) in registeredColumns)
                {
                    if (columnContents.TryGetValue(key, out var group))
                    {
                        group.Remove(content);
                        if (group.Count == 0)
                            columnContents.Remove(key);
                    }
                }
                registeredColumns.Clear();
            }
            base.Dispose(isDisposing);
        }
    }
}
