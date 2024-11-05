// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Overlays.Toolbar;
using osu.Framework.Bindables;
using osu.Game.Scoring;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;

namespace PerformanceCalculatorGUI.Components
{
    public partial class RealmScreenSettingsPopover : OsuPopover
    {
        private readonly Bindable<bool>[] bindables;

        public RealmScreenSettingsPopover(Bindable<bool>[] bindables)
        {
            this.bindables = bindables;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                AutoSizeAxes = Axes.Y,
                Width = 500,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Direction = FillDirection.Vertical,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(18),
                        Children = new Drawable[]
                        {
                            new OsuCheckbox
                            {
                                LabelText = "Calculate Ranked Maps (ranked & approved)",
                                Current = { BindTarget = bindables[0] }
                            },
                            new OsuCheckbox
                            {
                                LabelText = "Calculate Unranked Maps (everything else)",
                                Current = { BindTarget = bindables[1] }
                            },
                            new OsuCheckbox
                            {
                                LabelText = "Calculate Unsubmitted Scores (on local maps for example)",
                                Current = { BindTarget = bindables[2] }
                            },
                            new OsuCheckbox
                            {
                                LabelText = "Calculate Unranked Mods (RX and AP are excluded anyway)",
                                Current = { BindTarget = bindables[3] }
                            },
                            new OsuCheckbox
                            {
                                LabelText = "Enable Scorev1-ning on legacy scores",
                                Current = { BindTarget = bindables[4] }
                            },
                            new OsuCheckbox
                            {
                                LabelText = "Export historic data into .csv",
                                Current = { BindTarget = bindables[5] }
                            },
                        }
                    }
                }
            });
        }
    }

    public partial class RealmSettingsMenu : ToolbarButton, IHasPopover
    {
        private readonly Bindable<bool> calculateRankedMaps = new(true);
        private readonly Bindable<bool> calculateUnrankedMaps = new(false);

        private readonly Bindable<bool> calculateUnsubmittedScores = new(true);
        private readonly Bindable<bool> calculateUnrankedMods = new(true);

        private readonly Bindable<bool> enableScorev1ning = new(false);
        private readonly Bindable<bool> exportInCsv = new(false);
        public bool IsScorev1ningEnabled => enableScorev1ning.Value;
        public bool ExportInCSV => exportInCsv.Value;
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public RealmSettingsMenu()
        {
            TooltipMain = "Calculation Settings";

            SetIcon(new ScreenSelectionButtonIcon(FontAwesome.Solid.Cog) { IconSize = new Vector2(70) });
        }

        public bool ShouldBeFiltered(ScoreInfo score)
        {
            if (score.Mods.Any(h => h is OsuModRelax || h is OsuModAutopilot))
                return true;

            if (!calculateRankedMaps.Value && score.BeatmapInfo.Status.GrantsPerformancePoints())
                return true;

            if (!calculateUnrankedMaps.Value && !score.BeatmapInfo.Status.GrantsPerformancePoints())
                return true;

            if (!calculateUnrankedMods.Value)
            {
                // Check for legacy score cuz CL is unranked
                if (!score.Mods.All(m => m.Ranked || (score.IsLegacyScore && m is OsuModClassic)))
                    return true;
            }

            if (!calculateUnsubmittedScores.Value)
            {
                if (score.OnlineID <= 0 && score.LegacyOnlineID <= 0)
                    return true;
            }

            return false;
        }

        public Popover GetPopover() => new RealmScreenSettingsPopover(
            new[] { calculateRankedMaps, calculateUnrankedMaps, calculateUnsubmittedScores, calculateUnrankedMods, enableScorev1ning, exportInCsv });

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return base.OnClick(e);
        }
    }
}
