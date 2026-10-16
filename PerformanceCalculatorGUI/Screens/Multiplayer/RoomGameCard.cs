// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Scoring;
using osuTK;
using PerformanceCalculatorGUI.Screens.Multiplayer.ScoreConverters;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public partial class RoomGameCard : Container
    {
        // Data
        private readonly RoomHistory history;
        private readonly RoomGame game;
        public readonly BindableBool IncludeInSummary = new(true);
        public ScoreType CurrentType { get; private set; } = ScoreType.None;
        public bool HasTeams => game.HasTeams;

        // Children
        public FillFlowContainer<RoomScoreCard> ScoreCards { get; private set; } = null!;

        private TeamFooter? teamFooter;
        private Box cardDim = null!;
        private readonly Dictionary<string, List<Drawable>> columnContents = new();

        // Dependencies
        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

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

        // Returns converted score for the given team
        public long TeamTotal(string team) => ScoreCards.Where(s => s.Team == team).Sum(s => s.CurrentTotalScore);

        public void ConvertTo(ScoreType type)
        {
            CurrentType = type;

            foreach (RoomScoreCard scoreCard in ScoreCards.Children)
            {
                scoreCard.ConvertTo(type);
            }

            teamFooter?.Calculate();

            // Reorder cards
            IEnumerable<RoomScoreCard> ordered;

            if (game.HasTeams)
            {
                string winningTeam = TeamTotal("blue") > TeamTotal("red") ? "blue" : "red";

                ordered = ScoreCards
                    .OrderBy(card =>
                    {
                        string? team = card.Team;
                        return team == winningTeam ? 0 : team is "red" or "blue" ? 1 : 2;
                    })
                    .ThenByDescending(card => card.CurrentTotalScore)
                    .ThenBy(card => card.Score.UserID);
            }
            else
            {
                ordered = ScoreCards
                    .OrderByDescending(card => card.CurrentTotalScore)
                    .ThenBy(card => card.Score.UserID);
            }

            int position = 0;

            foreach (var card in ordered)
                ScoreCards.SetLayoutPosition(card, position++);
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, GameHost host)
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
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background4
                },
                cardDim = new Box {

                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black,
                    Alpha = 0,
                    Depth = -1,
                },
                content
            };

            content.Add(createHeader(colours, textures, host));

            if (game.Scores.Count == 0)
            {
                content.Add(text("No scores were submitted.", 16));
                return;
            }

            ScoreCards = new FillFlowContainer<RoomScoreCard>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Left = 8, Right = 8, Bottom = 8 },
                Spacing = new Vector2(0, 5)
            };

            content.Add(ScoreCards);
            var ruleset = RulesetHelper.GetRulesetFromLegacyID(game.RulesetId);

            if (game.HasTeams)
            {
                string[] teams = game.TeamTotal("blue") > game.TeamTotal("red")
                    ? new[] { "blue", "red" } : new[] { "red", "blue" };

                foreach (string team in teams)
                    foreach (var score in game.Scores.Where(s => game.TeamFor(s) == team).OrderByDescending(s => s.TotalScore))
                        if (hasMap(score))
                            ScoreCards.Add(new RoomScoreCard(history, game, score, columnContents));

                foreach (var score in game.Scores.Where(s => game.TeamFor(s) is not ("red" or "blue")).OrderByDescending(s => s.TotalScore))
                    if (hasMap(score))
                        ScoreCards.Add(new RoomScoreCard(history, game, score, columnContents));

                content.Add(teamFooter = new TeamFooter(this));
            }
            else
            {
                foreach (var score in game.Scores.OrderByDescending(s => s.TotalScore))
                    if (hasMap(score))
                        ScoreCards.Add(new RoomScoreCard(history, game, score, columnContents));
            }

            IncludeInSummary.BindValueChanged(c => cardDim.Alpha = c.NewValue ? 0 : 0.5f, true);
        }

        private bool hasMap(SoloScoreInfo score)
        {
            return history.Beatmaps.GetValueOrDefault(score.BeatmapID) != null;
        }

        private OsuClickableContainer createHeader(OverlayColourProvider colours, LargeTextureStore textures, GameHost host)
        {
            history.Beatmaps.TryGetValue(game.BeatmapId, out var beatmap);

            var header = new OsuClickableContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 100,
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

            var includeCheckbox = new OsuCheckbox
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.Y,
                Width = 240,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                LabelText = "Include in match summary"
            };

            includeCheckbox.Current.BindTo(IncludeInSummary);

            header.Add(new Container
            {
                Origin = Anchor.BottomRight,
                Anchor = Anchor.BottomRight,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(12),
                Child = includeCheckbox
            });

            return header;
        }

        private static OsuSpriteText text(string value, float size, Colour4? colour = null) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = value,
            Font = OsuFont.GetFont(size: size),
            Colour = colour ?? Colour4.White
        };

        private partial class TeamFooter : FillFlowContainer
        {
            public RoomGameCard Owner;

            public Container TeamResults { get; private set; }
            public OsuSpriteText MatchResult { get; private set; }

            public TeamFooter(RoomGameCard owner)
            {
                Owner = owner;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;

                Children = new Drawable[]
                {
                    TeamResults = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 60,
                        Padding = new MarginPadding { Left = 18, Right = 18, Top = 0, Bottom = 0 },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = owner.colours.Background5 },
                            MatchResult = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = OsuFont.GetFont(size: 17, weight: FontWeight.Bold),
                            }
                        }
                    }
                };

                Calculate();
            }

            public void Calculate()
            {
                long red = Owner.TeamTotal("red");
                long blue = Owner.TeamTotal("blue");

                TeamResults.Children = new Drawable[]
                {
                    createTeamTotal("Red Team", red, "red", Anchor.TopLeft),
                    createTeamTotal("Blue Team", blue, "blue", Anchor.TopRight)
                };

                MatchResult.Text = red == blue ? "Draw" : $"{(red > blue ? "Red" : "Blue")} Team wins by {ScoreConverter.FormatScore(Math.Abs(red - blue), Owner.CurrentType)}";
            }

            private Drawable createTeamTotal(string name, long total, string team, Anchor anchor) => new FillFlowContainer
            {
                Anchor = anchor,
                Origin = anchor,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new OsuSpriteText { Anchor = anchor, Origin = anchor, Text = name, Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold), Colour = TeamColour(team) },
                    new OsuSpriteText { Anchor = anchor, Origin = anchor, Text = ScoreConverter.FormatScore(total, Owner.CurrentType), Font = OsuFont.GetFont(size: 26) }
                }
            };
        }

        public static Colour4 TeamColour(string team) => team == "red" ? Colour4.FromHex("FF7799") : Colour4.FromHex("77BBFF");
    }
}
