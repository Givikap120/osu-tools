// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Screens.MyCollections;
using PerformanceCalculatorGUI.Screens.Profile;

namespace PerformanceCalculatorGUI.Components
{
    public class ExtendedScore
    {
        public ScoreInfo? ScoreInfoSource { get; set; }
        public SoloScoreInfo SoloScore { get; }
        public double? LivePP { get; set; }

        public Bindable<int> Position { get; } = new Bindable<int>();
        public Bindable<int> PositionChange { get; } = new Bindable<int>();

        public PerformanceAttributes? PerformanceAttributes { get; }
        public DifficultyAttributes DifficultyAttributes { get; }

        public ExtendedScore(SoloScoreInfo score, double? livePP, DifficultyAttributes difficultyAttributes, PerformanceAttributes? performanceAttributes)
        {
            SoloScore = score;
            PerformanceAttributes = performanceAttributes;
            DifficultyAttributes = difficultyAttributes;
            LivePP = livePP;
        }

        public ExtendedScore(SoloScoreInfo score, DifficultyAttributes difficultyAttributes, PerformanceAttributes? performanceAttributes)
        {
            SoloScore = score;
            PerformanceAttributes = performanceAttributes;
            DifficultyAttributes = difficultyAttributes;
            LivePP = score.PP;
        }

        public ExtendedScore(ScoreInfo score, double? livePP, DifficultyAttributes difficultyAttributes, PerformanceAttributes performanceAttributes)
        {
            ScoreInfoSource = score;
            SoloScore = toSoloScoreInfo(score);
            PerformanceAttributes = performanceAttributes;
            DifficultyAttributes = difficultyAttributes;
            LivePP = livePP;
        }

        public ExtendedScore(ScoreInfo score, DifficultyAttributes difficultyAttributes, PerformanceAttributes performanceAttributes)
        {
            ScoreInfoSource = score;
            SoloScore = toSoloScoreInfo(score);
            DifficultyAttributes = difficultyAttributes;
            PerformanceAttributes = performanceAttributes;
        }

        private static SoloScoreInfo toSoloScoreInfo(ScoreInfo score)
        {
            if (score.BeatmapInfo == null) throw new ArgumentNullException("No beatmap info found");

            var dummySet = new APIBeatmapSet
            {
                Title = score.BeatmapInfo.Metadata.Title,
                TitleUnicode = score.BeatmapInfo.Metadata.TitleUnicode,
                Artist = score.BeatmapInfo.Metadata.Artist,
                ArtistUnicode = score.BeatmapInfo.Metadata.ArtistUnicode,
            };
            var dummyBeatmap = new APIBeatmap
            {
                OnlineID = score.BeatmapInfo.OnlineID,
                DifficultyName = score.BeatmapInfo.DifficultyName,
            };
            var soloScoreInfo = new SoloScoreInfo
            {
                PP = score.PP,
                Accuracy = score.Accuracy,
                Rank = score.Rank,
                Statistics = score.Statistics,
                MaxCombo = score.MaxCombo,
                Mods = score.APIMods,
                BeatmapID = score.BeatmapInfo.OnlineID,
                Beatmap = dummyBeatmap,
                EndedAt = score.Date,
                BeatmapSet = dummySet,
                UserID = score.UserID,
                User = score.User,
                ID = score?.OnlineID > 0 ? (ulong?)score.OnlineID : 0,
                LegacyScoreId = score?.LegacyOnlineID > 0 ? (ulong?)score.LegacyOnlineID : 0,
            };

            return soloScoreInfo;
        }
    }

    public partial class ExtendedProfileItemContainer : ProfileItemContainer
    {
        public Action? OnHoverAction { get; set; }
        public Action? OnUnhoverAction { get; set; }

        public ExtendedProfileItemContainer()
        {
            CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS;
        }

        protected override bool OnHover(HoverEvent e)
        {
            OnHoverAction?.Invoke();
            base.OnHover(e);
            return false;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            OnUnhoverAction?.Invoke();
            base.OnHoverLost(e);
        }
    }

    public partial class ExtendedProfileScore : CompositeDrawable, IHasPopover
    {
        private const int height = 35;
        private const int avatar_size = 35;
        private const int performance_width = 100;
        private const int rank_difference_width = 35;
        private const int small_text_font_size = 11;

        private const float performance_background_shear = 0.45f;

        public readonly ExtendedScore Score;

        public readonly bool ShowAvatar;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuSpriteText positionChangeText = null!;

        private OsuSpriteText livePpDisplay = null!;
        private OsuSpriteText differenceDisplay = null!;

        private DifferenceMode differenceMode;

        public DifferenceMode DifferenceMode
        {
            get => differenceMode;
            set
            {
                differenceMode = value;
                updateLabels();
            }
        }

        public ExtendedProfileScore(ExtendedScore score, bool showAvatar = false)
        {
            Score = score;
            ShowAvatar = showAvatar;

            RelativeSizeAxes = Axes.X;
            Height = height;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, RulesetStore rulesets)
        {
            int avatarPadding = ShowAvatar ? avatar_size : 0;
            int rankDifferenceWidth = ShowAvatar ? 8 : rank_difference_width;
            var scoreRuleset = rulesets.GetRuleset(Score.SoloScore.RulesetID)?.CreateInstance() ?? throw new InvalidOperationException();

            AddInternal(new ExtendedProfileItemContainer
            {
                OnHoverAction = () =>
                {
                    positionChangeText.Text = $"#{Score.Position.Value}";
                },
                OnUnhoverAction = () =>
                {
                    positionChangeText.Text = $"{Score.PositionChange.Value:+0;-0;-}";
                },
                Children = new[]
                {
                    ShowAvatar
                        ? new ClickableAvatar(Score.SoloScore.User, true)
                        {
                            Masking = true,
                            CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS,
                            Size = new Vector2(avatar_size),
                            Action = () => { host.OpenUrlExternally($"https://osu.ppy.sh/users/{Score.SoloScore.User?.Id}"); }
                        }
                        : Empty(),
                    new Container
                    {
                        Name = "Rank difference",
                        RelativeSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Alpha = ShowAvatar ? 0 : 1,
                        Width = rankDifferenceWidth,
                        Margin = new MarginPadding { Left = avatarPadding },
                        Child = positionChangeText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = colourProvider.Light1,
                            Text = $"{Score.PositionChange.Value:+0;-0;-}"
                        }
                    },
                    new Container
                    {
                        Name = "Score info",
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = rankDifferenceWidth + avatarPadding, Right = performance_width },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Padding = new MarginPadding { Top = 2 },
                                        Children = new Drawable[]
                                        {
                                            new UpdateableRank(Score.SoloScore.Rank)
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Size = new Vector2(40, 12),
                                            },
                                            new TinyStarRatingDisplay(Score.DifficultyAttributes)
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                            },
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 0.5f),
                                        Children = new Drawable[]
                                        {
                                            new ScoreBeatmapMetadataContainer(Score.SoloScore.Beatmap),
                                            new FillFlowContainer
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(15, 0),
                                                Children = new Drawable[]
                                                {
                                                    new OsuSpriteText
                                                    {
                                                        Text = $"{Score.SoloScore.Beatmap?.DifficultyName}",
                                                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                                                        Colour = colours.Yellow
                                                    },
                                                    new DrawableDate(Score.SoloScore.EndedAt, 12)
                                                    {
                                                        Colour = colourProvider.Foreground1
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        AutoSizeAxes = Axes.X,
                                        RelativeSizeAxes = Axes.Y,
                                        Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Child = new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Vertical,
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(10, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Width = getStatisticsWidth(scoreRuleset),
                                                            RelativeSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Children = new Drawable[]
                                                            {
                                                                new OsuSpriteText
                                                                {
                                                                    Text = Score.SoloScore.Accuracy.FormatAccuracy(),
                                                                    Font = OsuFont.GetFont(weight: FontWeight.Bold, italics: true),
                                                                    Colour = colours.Yellow,
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre
                                                                },
                                                                new FillFlowContainer
                                                                {
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(3, 0),
                                                                    Children = new[]
                                                                    {
                                                                        formatCombo(),
                                                                        new OsuSpriteText
                                                                        {
                                                                            Text = $"{{ {formatStatistics(Score.SoloScore.Statistics, scoreRuleset)} }}",
                                                                            Font = OsuFont.GetFont(size: small_text_font_size, weight: FontWeight.Regular),
                                                                            Colour = colourProvider.Light2,
                                                                            Anchor = Anchor.TopCentre,
                                                                            Origin = Anchor.TopCentre
                                                                        },
                                                                    }
                                                                }
                                                            }
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Width = 60,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Children = new Drawable[]
                                                            {
                                                                new Container
                                                                {
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Child = livePpDisplay = new OsuSpriteText
                                                                    {
                                                                        Font = OsuFont.GetFont(weight: FontWeight.Bold),
                                                                        Text = Score.LivePP != null ? $"{Score.LivePP:0}pp" : "- pp"
                                                                    },
                                                                },
                                                                new OsuSpriteText
                                                                {
                                                                    Font = OsuFont.GetFont(size: small_text_font_size),
                                                                    Text = "live"
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(2),
                                        Children = Score.SoloScore.Mods.Select(mod => new ModIcon(mod.ToMod(scoreRuleset))
                                        {
                                            Scale = new Vector2(0.35f)
                                        }).ToList(),
                                    }
                                }
                            }
                        }
                    },
                    new Container
                    {
                        Name = "Performance",
                        RelativeSizeAxes = Axes.Y,
                        Width = performance_width,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.5f,
                                Colour = colourProvider.Background4,
                                Shear = new Vector2(-performance_background_shear, 0),
                                EdgeSmoothness = new Vector2(2, 0),
                            },
                            new Box
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                RelativeSizeAxes = Axes.Both,
                                RelativePositionAxes = Axes.Y,
                                Height = -0.5f,
                                Position = new Vector2(0, 1),
                                Colour = colourProvider.Background4,
                                Shear = new Vector2(performance_background_shear, 0),
                                EdgeSmoothness = new Vector2(2, 0),
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Padding = new MarginPadding
                                {
                                    Vertical = 5,
                                    Left = 30,
                                    Right = 20
                                },
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new ExtendedOsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(weight: FontWeight.Bold),
                                        Text = $"{Score.PerformanceAttributes?.Total:0}pp",
                                        Colour = colourProvider.Highlight1,
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        TooltipContent = $"{AttributeConversion.ToReadableString(Score.PerformanceAttributes)}"
                                    },
                                    differenceDisplay = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: small_text_font_size),
                                        Text = $"{Score.PerformanceAttributes?.Total - Score.LivePP:+0.0;-0.0;-}",
                                        Colour = getPpDifferenceColor(),
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre
                                    }
                                }
                            }
                        }
                    }
                }
            });

            updateLabels();

            Score.PositionChange.BindValueChanged(v => { positionChangeText.Text = $"{v.NewValue:+0;-0;-}"; });
        }

        public Func<Popover>? PopoverMaker { get; set; } = null;
        public Popover? GetPopover() => PopoverMaker?.Invoke();

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (PopoverMaker != null && e.Button == MouseButton.Right)
                this.ShowPopover();

            return base.OnMouseDown(e);
        }
        public double LivePP
        {
            set
            {
                Score.LivePP = value;
                updateLabels();
            }
        }

        private void updateLabels()
        {
            if (livePpDisplay == null || differenceDisplay == null || Score.PerformanceAttributes == null) return;

            livePpDisplay.Text = $"{Score.LivePP:0}pp";

            switch (differenceMode)
            {
                case DifferenceMode.Delta:
                    double? deltaDifference = Score.PerformanceAttributes.Total - Score.LivePP;
                    double? percentageDifference = Score.PerformanceAttributes.Total / Score.LivePP - 1;
                    differenceDisplay.Text = $"{deltaDifference:+0.0;-0.0;-}";
                    differenceDisplay.Colour = getColorForPercentageDifference(percentageDifference ?? 0);
                    break;

                case DifferenceMode.Percent:
                    percentageDifference = Score.PerformanceAttributes.Total / Score.LivePP - 1;
                    differenceDisplay.Text = $"{percentageDifference:+0.0%;-0.0%;-}";
                    differenceDisplay.Colour = getColorForPercentageDifference(percentageDifference ?? 0);
                    break;
            }
        }

        private Color4 getPpDifferenceColor()
        {
            double difference = Score.PerformanceAttributes?.Total - Score.LivePP ?? 0;
            var baseColor = colourProvider.Light1;

            return difference switch
            {
                < 0 => Interpolation.ValueAt(difference, baseColor, Color4.OrangeRed, 0, -200),
                > 0 => Interpolation.ValueAt(difference, baseColor, Color4.Lime, 0, 200),
                _ => baseColor
            };
        }

        private OsuSpriteText formatCombo()
        {
            bool isFullCombo = Score.SoloScore.MaxCombo == Score.DifficultyAttributes.MaxCombo;

            return new ExtendedOsuSpriteText
            {
                Text = $"{Score.SoloScore.MaxCombo}x",
                Font = OsuFont.GetFont(size: small_text_font_size, weight: FontWeight.Regular),
                Colour = isFullCombo ? colours.GreenLight : colourProvider.Light2,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                TooltipContent = $"{Score.SoloScore.MaxCombo} / {Score.DifficultyAttributes.MaxCombo}x"
            };
        }

        private static int getStatisticsWidth(Ruleset ruleset)
        {
            var rulesetHitResults = ruleset.GetHitResults().Where(x => x.result.IsBasic()).ToArray();

            return Math.Max(80, rulesetHitResults.Length * 15 + 50); // 50px are reserved for the combo
        }

        private static string formatStatistics(Dictionary<HitResult, int> statistics, Ruleset ruleset)
        {
            var rulesetHitResults = ruleset.GetHitResults().Where(x => x.result.IsBasic()).ToArray();

            var statisticsBuilder = new StringBuilder();

            for (int i = 0; i < rulesetHitResults.Length; i++)
            {
                statisticsBuilder.Append(statistics.GetValueOrDefault(rulesetHitResults[i].result));

                if (i < rulesetHitResults.Length - 1)
                    statisticsBuilder.Append(" / ");
            }

            return statisticsBuilder.ToString();
        }

        private static Colour4 colourLerp(Colour4 from, Colour4 to, float t)
        {
            return new Colour4(
                from.R + (to.R - from.R) * t,
                from.G + (to.G - from.G) * t,
                from.B + (to.B - from.B) * t,
                from.A + (to.A - from.A) * t
            );
        }

        private Colour4 getColorForPpDifference(double ppDifference)
        {
            double t = Math.Clamp(ppDifference / 100.0, -1.0, 1.0);

            if (t < 0)
                return colourLerp(Colour4.Red, colourProvider.Light1, (float)(t + 1.0));
            else
                return colourLerp(colourProvider.Light1, Colour4.Lime, (float)t);
        }

        private Colour4 getColorForPercentageDifference(double percentageDifference)
        {
            double t = Math.Clamp(percentageDifference / 0.15, -1.0, 1.0);

            if (t < 0)
                return colourLerp(Colour4.Red, colourProvider.Light1, (float)(t + 1.0));
            else
                return colourLerp(colourProvider.Light1, Colour4.Lime, (float)t);
        }

        private partial class ScoreBeatmapMetadataContainer : OsuHoverContainer
        {
            private readonly IBeatmapInfo? beatmapInfo;

            public ScoreBeatmapMetadataContainer(IBeatmapInfo? beatmapInfo)
            {
                this.beatmapInfo = beatmapInfo;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader(true)]
            private void load(GameHost host)
            {
                Action = () =>
                {
                    host.OpenUrlExternally($"https://osu.ppy.sh/b/{beatmapInfo?.OnlineID}");
                };

                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = new RomanisableString(beatmapInfo?.Metadata.TitleUnicode, beatmapInfo?.Metadata.Title),
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold, italics: true)
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = " by ",
                            Font = OsuFont.GetFont(size: 12, italics: true)
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = new RomanisableString(beatmapInfo?.Metadata.ArtistUnicode, beatmapInfo?.Metadata.Artist),
                            Font = OsuFont.GetFont(size: 12, italics: true)
                        },
                    }
                };
            }
        }
    }

    public enum DifferenceMode
    {
        Delta,
        Percent
    }

    public static class DifferenceModeExtensions
    {
        public static DifferenceMode GetDifferenceMode(this ProfileSortCriteria sorting)
            => sorting == ProfileSortCriteria.Percentage ? DifferenceMode.Percent : DifferenceMode.Delta;

        public static DifferenceMode GetDifferenceMode(this MyCollectionSortCriteria sorting)
            => sorting == MyCollectionSortCriteria.Percentage ? DifferenceMode.Percent : DifferenceMode.Delta;
    }
}
