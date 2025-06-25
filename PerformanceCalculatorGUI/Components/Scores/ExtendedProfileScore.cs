// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace PerformanceCalculatorGUI.Components.Scores
{
    public class ExtendedProfileScore : ProfileScore
    {
        public double? LivePP { get; set; }
        public Bindable<int> PositionChange { get; } = new Bindable<int>();

        public ExtendedProfileScore(SoloScoreInfo score, double? livePP, DifficultyAttributes difficultyAttributes, PerformanceAttributes performanceAttributes)
            : base(score, difficultyAttributes, performanceAttributes)
        {
            LivePP = livePP;
        }

        public ExtendedProfileScore(ScoreInfo score, double? livePP, DifficultyAttributes difficultyAttributes, PerformanceAttributes performanceAttributes)
            : base(score, difficultyAttributes, performanceAttributes)
        {
            LivePP = livePP;
        }
    }

    public partial class DrawableExtendedProfileScore : DrawableProfileScore
    {
        protected new ExtendedProfileScore Score { get; }

        public DrawableExtendedProfileScore(ExtendedProfileScore score, bool showAvatar = false)
            : base(score, showAvatar)
        {
            Score = score;
        }

        private OsuSpriteText livePpDisplay;
        private OsuSpriteText differenceDisplay;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Score.Position.UnbindEvents();
            Score.PositionChange.BindValueChanged(v => { PositionText.Text = $"{v.NewValue:+0;-0;-}"; });
        }


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
            if (livePpDisplay == null || differenceDisplay == null) return;

            livePpDisplay.Text = $"{Score.LivePP:0}pp";

            switch (differenceMode)
            {
                case DifferenceMode.Delta:
                    double? deltaDifference = Score.PerformanceAttributes.Total - Score.LivePP;
                    differenceDisplay.Text = $"{deltaDifference:+0.0;-0.0;-}";
                    differenceDisplay.Colour = getColorForPpDifference(deltaDifference ?? 0);
                    break;

                case DifferenceMode.Percent:
                    double? percentageDifference = Score.PerformanceAttributes.Total / Score.LivePP - 1;
                    differenceDisplay.Text = $"{percentageDifference:+0.0%;-0.0%;-}";
                    differenceDisplay.Colour = getColorForPercentageDifference(percentageDifference ?? 0);
                    break;
            }
        }

        protected override Drawable[] CreateRightInfoContainerContent(RulesetStore rulesets)
        {
            return new Drawable[]
            {
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
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
                                Text = $"{Score.LivePP:0}pp"
                            },
                        },
                        new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: SMALL_TEXT_FONT_SIZE),
                            Text = "live"
                        }
                    }
                }
            }.Concat(base.CreateRightInfoContainerContent(rulesets)).ToArray();
        }

        protected override Drawable CreatePerformanceInfo()
        {
            var performanceContainer = new FillFlowContainer
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
                        Text = $"{Score.PerformanceAttributes.Total:0}pp",
                        Colour = ColourProvider.Highlight1,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        TooltipContent = $"{AttributeConversion.ToReadableString(Score.DifficultyAttributes, Score.PerformanceAttributes)}"
                    },
                    differenceDisplay = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: SMALL_TEXT_FONT_SIZE),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre
                    }
                }
            };

            updateLabels();

            return performanceContainer;
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
                return colourLerp(Colour4.Red, ColourProvider.Light1, (float)(t + 1.0));
            else
                return colourLerp(ColourProvider.Light1, Colour4.Lime, (float)t);
        }

        private Colour4 getColorForPercentageDifference(double percentageDifference)
        {
            double t = Math.Clamp(percentageDifference / 0.25, -1.0, 1.0);

            if (t < 0)
                return colourLerp(Colour4.Red, ColourProvider.Light1, (float)(t + 1.0));
            else
                return colourLerp(ColourProvider.Light1, Colour4.Lime, (float)t);
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
    }
}
