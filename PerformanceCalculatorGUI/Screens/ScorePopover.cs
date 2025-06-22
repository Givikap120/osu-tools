// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using PerformanceCalculatorGUI.Components.Scores;
using osu.Framework.Platform;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ScorePopover : OsuPopover
    {
        [Resolved]
        private GameHost host { get; set; }

        protected ProfileScore Score { get; private set; }

        public ScorePopover(ProfileScore score)
        {
            Score = score;
        }

        private Drawable[] createButtons()
        {
            List<Drawable> result = [];

            if (Score?.SoloScore.OnlineID > 1 || Score?.ScoreInfoSource.OnlineID > 1)
            {
                long onlineId = Math.Max(Score?.SoloScore.OnlineID ?? -1, Score?.ScoreInfoSource.OnlineID ?? -1);

                result.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Open score in browser",
                    Action = () =>
                    {
                        host.OpenUrlExternally($"https://osu.ppy.sh/scores/{onlineId}");
                        PopOut();
                    }
                });
            }

            if (Score?.SoloScore.LegacyScoreId > 1 || Score?.ScoreInfoSource.LegacyOnlineID > 1)
            {
                long onlineId = Math.Max((long?)Score.SoloScore.LegacyScoreId ?? -1, Score?.ScoreInfoSource.LegacyOnlineID ?? -1);
                int rulesetId = Score.SoloScore.RulesetID;
                string rulesetName = RulesetHelper.GetRulesetFromLegacyID(rulesetId).ShortName;

                result.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Open score in browser (legacy)",
                    Action = () =>
                    {
                        host.OpenUrlExternally($"https://osu.ppy.sh/scores/{rulesetName}/{onlineId}");
                        PopOut();
                    }
                });
            }

            return [.. result];
        }

        protected FillFlowContainer Buttons;


        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                AutoSizeAxes = Axes.Y,
                Width = 300,
                Children = new Drawable[]
                {
                        Buttons = new FillFlowContainer
                        {
                            Direction = FillDirection.Vertical,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Spacing = new Vector2(12),
                            Children = createButtons()
                        }
                }
            });
        }
    }
}
