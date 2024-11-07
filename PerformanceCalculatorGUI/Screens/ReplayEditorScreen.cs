// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Humanizer;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;
using osuTK;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;
using PerformanceCalculatorGUI.Screens.ObjectInspection;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ReplayEditorScreen : PerformanceCalculatorScreen
    {
        private ProcessorWorkingBeatmap beatmap;
        private Score score;

        private ExtendedUserModSelectOverlay userModsSelectOverlay;

        private GridContainer replayImportContainer;
        private LabelledTextBox replayFileTextBox;
        private LabelledTextBox replayIdTextBox;
        private SwitchButton replayImportTypeSwitch;

        private ReplayAttributeTextBox exportFilenameBox;
        private OsuButton exportReplayButton;

        private DifficultyAttributes difficultyAttributes;
        private FillFlowContainer performanceAttributesContainer;

        private PerformanceCalculator performanceCalculator;

        [Cached]
        private Bindable<DifficultyCalculator> difficultyCalculator = new Bindable<DifficultyCalculator>();

        private FillFlowContainer beatmapDataContainer;
        private Container beatmapTitle;

        private ModDisplay modDisplay;

        private ObjectInspector objectInspector;

        private BufferedContainer background;

        private ScheduledDelegate debouncedPerformanceUpdate;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private AudioManager audio { get; set; }

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> appliedMods { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private LargeTextureStore textures { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        [Cached] 
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        public override bool ShouldShowConfirmationDialogOnSwitch => beatmap != null;

        private const int file_selection_container_height = 40;
        private const int map_title_container_height = 40;
        private const float mod_selection_container_scale = 0.7f;

        // Replay attributes

        private ReplayAttributeTextBox playerBox;
        private OsuCheckbox addMarkCheckbox;

        private ReplayAttributeNumberBox versionBox;
        private OsuCheckbox useDefaultVersionCheckbox;

        private ReplayAttributeTextBox beatmapHashBox;
        private OsuButton changeBeatmapHashButton;

        private GridContainer greatGekiContainer;
        private ReplayAttributeNumberBox greatBox;
        private ReplayAttributeNumberBox gekiBox;

        private GridContainer goodKatuContainer;
        private ReplayAttributeNumberBox goodBox;
        private ReplayAttributeNumberBox katuBox;

        private ReplayAttributeNumberBox mehBox;
        private ReplayAttributeNumberBox missBox;

        private ReplayAttributeNumberBox scoreBox;
        private ReplayAttributeNumberBox comboBox;

        private ReplayAttributeTextBox dateBox;
        private ReplayAttributeNumberBox scoreIDBox;

        // Lazer-specific

        private ReplayAttributeNumberBox lazerScoreIDBox;
        private ReplayAttributeTextBox clientVersionBox;

        private StatisticsContainer statisticsContainer;

        private static OsuCheckbox createCheckBox(string text, bool defaultValue = true) => new OsuCheckbox(nubOnRight: false)
        {
            LabelText = text,
            Current = { Value = defaultValue },
            Origin = Anchor.Centre,
            Anchor = Anchor.Centre
        };
        private static LabelledTextBox createTextDisplay(string name, string placeholder = "") => new LabelledTextBox
        {
            RelativeSizeAxes = Axes.X,
            Anchor = Anchor.TopLeft,
            Label = name,
            PlaceholderText = placeholder
        };

        private static GridContainer createDoubleDisplay(Drawable[] content) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions =
            [
                new Dimension(),
                new Dimension()
            ],
            RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
            Content = new[]
            {
                content
            }
        };

        private static GridContainer createDoubleDisplay(Drawable[] content, Dimension[] dimensions) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = dimensions,
            RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
            Content = new[]
            {
                content
            }
        };

        public ReplayEditorScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour osuColour)
        {
            InternalChildren = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, file_selection_container_height), new Dimension(GridSizeMode.Absolute, map_title_container_height), new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            replayImportContainer = new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute),
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        replayFileTextBox = new FileChooserLabelledTextBox(configManager.GetBindable<string>(Settings.ReplayPath), ".osr")
                                        {
                                            Label = "Replay File",
                                            FixedLabelWidth = 100f,
                                            PlaceholderText = "Click to select a replay file"
                                        },
                                        replayIdTextBox = new LimitedLabelledNumberBox
                                        {
                                            Label = "Replay ID",
                                            FixedLabelWidth = 100f,
                                            PlaceholderText = "Enter replay ID",
                                            CommitOnFocusLoss = false
                                        },
                                        replayImportTypeSwitch = new SwitchButton
                                        {
                                            Width = 80,
                                            Height = file_selection_container_height
                                        }
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            beatmapTitle = new Container
                            {
                                Name = "Replay title",
                                RelativeSizeAxes = Axes.Both
                            }
                        },
                        new Drawable[]
                        {
                            beatmapDataContainer = new FillFlowContainer
                            {
                                Name = "Replay data",
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Children = new Drawable[]
                                {
                                    new OsuScrollContainer(Direction.Vertical)
                                    {
                                        Name = "Score params",
                                        RelativeSizeAxes = Axes.Both,
                                        Width = 0.5f,
                                        Child = new FillFlowContainer
                                        {
                                            Padding = new MarginPadding { Left = 10f, Right = 15.0f, Vertical = 5f },
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2f),
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding { Left = 10f, Top = 5f, Bottom = 10.0f },
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Score info"
                                                },
                                                createDoubleDisplay(
                                                [
                                                    playerBox = new ReplayAttributeTextBox("Player Name"),
                                                    addMarkCheckbox = createCheckBox("Mark name as (edited)")
                                                ]),
                                                createDoubleDisplay(
                                                [
                                                    versionBox = new ReplayAttributeNumberBox("Score version"),
                                                    useDefaultVersionCheckbox = createCheckBox("Use default version export")
                                                ]),
                                                createDoubleDisplay(
                                                [
                                                    beatmapHashBox = new ReplayAttributeTextBox("Beatmap Hash"),
                                                    changeBeatmapHashButton = new RoundedButton
                                                    {
                                                        Text = "Change hash from beatmap file",
                                                        Width = 250,
                                                        BackgroundColour = colourProvider.Background1
                                                    },
                                                ], [new Dimension(), new Dimension(GridSizeMode.AutoSize)]),
                                                greatGekiContainer = createDoubleDisplay(
                                                [
                                                    greatBox = new ReplayAttributeNumberBox("Great"),
                                                    gekiBox = new ReplayAttributeNumberBox("Perfect")
                                                ]),
                                                goodKatuContainer = createDoubleDisplay(
                                                [
                                                    goodBox = new ReplayAttributeNumberBox("Good"),
                                                    katuBox = new ReplayAttributeNumberBox("Ok")
                                                ]),
                                                createDoubleDisplay(
                                                [
                                                    mehBox = new ReplayAttributeNumberBox("Meh"),
                                                    missBox = new ReplayAttributeNumberBox("Miss")
                                                ]),
                                                createDoubleDisplay(
                                                [
                                                    comboBox = new ReplayAttributeNumberBox("Combo"),
                                                    scoreBox = new ReplayAttributeNumberBox("Score").With(b =>
                                                    {
                                                        b.Value.Value = 1000000;
                                                    }),
                                                ]),
                                                createDoubleDisplay(
                                                [
                                                    dateBox = new ReplayAttributeTextBox("Date"),
                                                    scoreIDBox = new ReplayAttributeNumberBox("Online ID", minValue: -1)
                                                ]),
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding { Vertical = 10f, Bottom = 10.0f },
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Lazer-specific info"
                                                },
                                                createDoubleDisplay(
                                                [
                                                    lazerScoreIDBox = new ReplayAttributeNumberBox("Online ID (lazer)", minValue: -1),
                                                    clientVersionBox = new ReplayAttributeTextBox("Client version"),
                                                ]),
                                                statisticsContainer = new StatisticsContainer()
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(0, 2f)
                                                }
                                            }
                                        }
                                    },
                                    new OsuScrollContainer(Direction.Vertical)
                                    {
                                        Name = "Difficulty calculation results",
                                        RelativeSizeAxes = Axes.Both,
                                        Width = 0.5f,
                                        Child = new FillFlowContainer
                                        {
                                            Padding = new MarginPadding { Left = 10f, Right = 15.0f, Vertical = 5f },
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 5f),
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding(10.0f),
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Export settings"
                                                },
                                                createDoubleDisplay([
                                                    exportFilenameBox = new ReplayAttributeTextBox("Export filename", "Autogenerated"),
                                                    exportReplayButton = new RoundedButton
                                                    {
                                                        Text = "Export replay",
                                                        Width = 250,
                                                        BackgroundColour = colourProvider.Background1,
                                                        Action = exportCurrentReplay,
                                                        Origin = Anchor.Centre,
                                                        Anchor = Anchor.Centre
                                                    }
                                                    ]),
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding(10.0f),
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Mods Settings"
                                                },
                                                new FillFlowContainer
                                                {
                                                    Name = "Mods container",
                                                    Height = 40,
                                                    Direction = FillDirection.Horizontal,
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    AutoSizeAxes = Axes.Y,
                                                    Children = new Drawable[]
                                                    {
                                                        new RoundedButton
                                                        {
                                                            Width = 100,
                                                            Margin = new MarginPadding { Right = 5.0f },
                                                            Action = () => { userModsSelectOverlay.Show(); },
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "Mods"
                                                        },
                                                        modDisplay = new ModDisplay()
                                                    }
                                                },
                                                userModsSelectOverlay = new ExtendedUserModSelectOverlay
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 460 / mod_selection_container_scale,
                                                    Width = 1f / mod_selection_container_scale,
                                                    Scale = new Vector2(mod_selection_container_scale),
                                                    IsValidMod = mod => mod.HasImplementation && ModUtils.FlattenMod(mod).All(m => m.UserPlayable),
                                                    SelectedMods = { BindTarget = appliedMods }
                                                },
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding(10.0f),
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Performance Attributes"
                                                },
                                                performanceAttributesContainer = new FillFlowContainer
                                                {
                                                    Direction = FillDirection.Vertical,
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    AutoSizeAxes = Axes.Y,
                                                    Spacing = new Vector2(0, 2f)
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            beatmapDataContainer.Hide();
            userModsSelectOverlay.Hide();

            replayFileTextBox.Current.BindValueChanged(filePath => { changeReplay(filePath.NewValue); });
            replayIdTextBox.OnCommit += (_, _) => { changeReplay(replayIdTextBox.Current.Value); };

            replayImportTypeSwitch.Current.BindValueChanged(val =>
            {
                if (val.NewValue)
                {
                    replayImportContainer.ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize)
                    };

                    fixupTextBox(replayIdTextBox);
                }
                else
                {
                    replayImportContainer.ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize)
                    };
                }
            });

            goodBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            mehBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            missBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            comboBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            scoreBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());

            appliedMods.BindValueChanged(modsChanged);
            modDisplay.Current.BindTo(appliedMods);

            ruleset.BindValueChanged(_ =>
            {
                resetCalculations();
            });

            if (RuntimeInfo.IsDesktop)
            {
                HotReloadCallbackReceiver.CompilationFinished += _ => Schedule(() =>
                {
                    calculateDifficulty();
                    calculatePerformance();
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            modSettingChangeTracker?.Dispose();

            appliedMods.UnbindAll();
            appliedMods.Value = Array.Empty<Mod>();

            difficultyCalculator.UnbindAll();
            base.Dispose(isDisposing);
        }

        private ModSettingChangeTracker modSettingChangeTracker;
        private ScheduledDelegate debouncedStatisticsUpdate;

        private void modsChanged(ValueChangedEvent<IReadOnlyList<Mod>> mods)
        {
            modSettingChangeTracker?.Dispose();

            if (beatmap is null)
                return;

            modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
            modSettingChangeTracker.SettingChanged += m =>
            {
                debouncedStatisticsUpdate?.Cancel();
                debouncedStatisticsUpdate = Scheduler.AddDelayed(() =>
                {
                    calculateDifficulty();
                    calculatePerformance();
                }, 100);
            };

            calculateDifficulty();
            updateCombo(false);
            calculatePerformance();
        }

        private void resetScoreBeatmap()
        {
            score = null;
            beatmap = null;
            beatmapTitle.Clear();
            resetMods();
            beatmapDataContainer.Hide();

            if (background is not null)
            {
                RemoveInternal(background, true);
            }
        }

        private void changeReplay(string replay)
        {
            beatmapDataContainer.Hide();

            if (string.IsNullOrEmpty(replay))
            {
                showError("Empty replay path!");
                resetScoreBeatmap();
                return;
            }

            try
            {
                ExtendedScoreDecoder decoder = new ExtendedScoreDecoder(rulesets, beatmapManager, configManager);
                using (var stream = File.OpenRead(replay))
                    score = decoder.Parse(stream);

                beatmap = null;
                if (score.ScoreInfo.BeatmapInfo != null)
                {
                    WorkingBeatmap beatmap = beatmapManager.GetWorkingBeatmap(score.ScoreInfo.BeatmapInfo);
                    this.beatmap = new ProcessorWorkingBeatmap(beatmap.Beatmap, audioManager: audio);
                }
            }
            catch (Exception e)
            {
                showError(e);
                resetScoreBeatmap();
                return;
            }

            if (score is null)
                return;

            if (!score.ScoreInfo.Ruleset.Equals(ruleset.Value))
            {
                ruleset.Value = beatmap.BeatmapInfo.Ruleset;
            }
            else
            {
                resetCalculations();
            }

            updateInterfaceFromScore();

            if (beatmap != null) beatmapTitle.Add(new BeatmapCard(beatmap));

            loadBackground();

            beatmapDataContainer.Show();
        }

        private void updateInterfaceFromScore()
        {
            playerBox.Text = score.ScoreInfo.User.Username;
            if (playerBox.Text.EndsWith(" (edited)"))
                playerBox.Text = playerBox.Text[..^" (edited)".Length];

            versionBox.Text = score.ScoreInfo.TotalScoreVersion.ToString();

            beatmapHashBox.Text = score.ScoreInfo.BeatmapHash;

            greatBox.Text = score.ScoreInfo.GetCount300().ToString();
            gekiBox.Text = score.ScoreInfo.GetCountGeki().ToString();

            goodBox.Text = score.ScoreInfo.GetCount100().ToString();
            katuBox.Text = score.ScoreInfo.GetCountKatu().ToString();

            mehBox.Text = score.ScoreInfo.GetCount50().ToString();
            missBox.Text = score.ScoreInfo.GetCountMiss().ToString();

            scoreBox.Text = score.ScoreInfo.TotalScore.ToString();
            comboBox.Text = score.ScoreInfo.MaxCombo.ToString();

            dateBox.Text = score.ScoreInfo.Date.ToString();
            scoreIDBox.Text = score.ScoreInfo.LegacyOnlineID.ToString();

            appliedMods.Value = score.ScoreInfo.Mods;

            lazerScoreIDBox.Text = score.ScoreInfo.OnlineID.ToString();
            clientVersionBox.Text = score.ScoreInfo.ClientVersion;

            statisticsContainer.ImportContainer(score.ScoreInfo);
        }

        private static int safeParseInt(string s) => s == "" ? 0 :int.Parse(s);
        private static long safeParseLong(string s) => s == "" ? 0 : long.Parse(s);
        private Score getCurrentScore()
        {
            Score currentScore = score.DeepClone();

            currentScore.ScoreInfo.User.Username = playerBox.Text;

            currentScore.ScoreInfo.TotalScoreVersion = safeParseInt(versionBox.Text);

            currentScore.ScoreInfo.BeatmapHash = beatmapHashBox.Text;

            currentScore.ScoreInfo.SetCount300(safeParseInt(greatBox.Text));
            currentScore.ScoreInfo.SetCountGeki(safeParseInt(gekiBox.Text));

            currentScore.ScoreInfo.SetCount100(safeParseInt(goodBox.Text));
            currentScore.ScoreInfo.SetCountKatu(safeParseInt(katuBox.Text));

            currentScore.ScoreInfo.SetCount50(safeParseInt(mehBox.Text));
            currentScore.ScoreInfo.SetCountMiss(safeParseInt(missBox.Text));

            currentScore.ScoreInfo.TotalScore = safeParseLong(scoreBox.Text);
            currentScore.ScoreInfo.MaxCombo = safeParseInt(comboBox.Text);

            currentScore.ScoreInfo.Date = DateTimeOffset.Parse(dateBox.Text);
            currentScore.ScoreInfo.LegacyOnlineID = safeParseLong(scoreIDBox.Text);

            currentScore.ScoreInfo.Mods = appliedMods.Value.ToArray();

            currentScore.ScoreInfo.OnlineID = safeParseLong(lazerScoreIDBox.Text);
            currentScore.ScoreInfo.ClientVersion = clientVersionBox.Text;

            statisticsContainer.ExportContainer(ref currentScore.ScoreInfo);

            return currentScore;
        }

        private void exportCurrentReplay()
        {
            Score editedScore = getCurrentScore();

            string filename;
            if (exportFilenameBox.Current.Value == "")
            {
                string scoreString = editedScore.ScoreInfo.GetDisplayString();
                filename = $"(EDITED) {scoreString} ({editedScore.ScoreInfo.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";
            }
            else
            {
                filename = exportFilenameBox.Current.Value;
            }
            filename += ".osr";
            var filepath = configManager.GetBindable<string>(Settings.ReplayPath).Value + "\\" + filename;
            

            var encoder = new ExtendedScoreEncoder(editedScore, beatmap?.Beatmap);
            encoder.Export(filepath, useDefaulVersion: useDefaultVersionCheckbox.Current.Value, addNameMark: addMarkCheckbox.Current.Value);
        }

        private void createCalculators()
        {
            if (beatmap is null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();
            difficultyCalculator.Value = RulesetHelper.GetExtendedDifficultyCalculator(ruleset.Value, beatmap);
            performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
        }

        private void calculateDifficulty()
        {
            if (beatmap == null || difficultyCalculator.Value == null)
                return;

            try
            {
                difficultyAttributes = difficultyCalculator.Value.Calculate(appliedMods.Value);
            }
            catch (Exception e)
            {
                showError(e);
                resetScoreBeatmap();
                return;
            }
        }

        private void debouncedCalculatePerformance()
        {
            debouncedPerformanceUpdate?.Cancel();
            debouncedPerformanceUpdate = Scheduler.AddDelayed(calculatePerformance, 20);
        }

        private void calculatePerformance()
        {
            if (beatmap == null || difficultyAttributes == null)
                return;

            int? countGood = null, countMeh = null;

            countGood = goodBox.Value.Value;
            countMeh = mehBox.Value.Value;
            double accuracy = 1;

            int sliderbreaks = 0;
            int sliderendMisses = 0;

            var score = RulesetHelper.AdjustManiaScore(scoreBox.Value.Value, appliedMods.Value);

            try
            {
                var beatmap = this.beatmap.GetPlayableBeatmap(ruleset.Value, appliedMods.Value);

                Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>();

                if (ruleset.Value.OnlineID != -1)
                {
                    // official rulesets can generate more precise hits from accuracy
                    statistics = RulesetHelper.GenerateHitResultsForRuleset(ruleset.Value, 100, beatmap, missBox.Value.Value, countMeh, countGood, sliderbreaks, sliderendMisses);
                    accuracy = RulesetHelper.GetAccuracyForRuleset(ruleset.Value, statistics);
                }

                var ppAttributes = performanceCalculator?.Calculate(new ScoreInfo(beatmap.BeatmapInfo, ruleset.Value)
                {
                    Accuracy = accuracy,
                    MaxCombo = comboBox.Value.Value,
                    Statistics = statistics,
                    Mods = appliedMods.Value.ToArray(),
                    TotalScore = score,
                    Ruleset = ruleset.Value
                }, difficultyAttributes);

                performanceAttributesContainer.Children = AttributeConversion.ToDictionary(ppAttributes).Select(x =>
                    new ExtendedLabelledTextBox
                    {
                        ReadOnly = true,
                        Label = x.Key.Humanize().ToLowerInvariant(),
                        Text = FormattableString.Invariant($"{x.Value:N2}")
                    }
                ).ToArray();
            }
            catch (Exception e)
            {
                showError(e);
                resetScoreBeatmap();
            }
        }

        private void populateScoreParams()
        {
            gekiBox.Hide();
            katuBox.Hide();
            if (ruleset.Value.ShortName == "osu" || ruleset.Value.ShortName == "taiko" || ruleset.Value.ShortName == "fruits")
            {
                updateCombo(true);
            }
            else if (ruleset.Value.ShortName == "mania")
            {
                gekiBox.Show();
                katuBox.Show();
            }
            else
            {
                gekiBox.Show();
                katuBox.Show();
                updateCombo(true);
            }
            updateAccuracyParams();
        }

        private void updateAccuracyParams()
        {
            greatBox.Label = ruleset.Value.ShortName switch
            {
                "osu" => "300s",
                "fruits" => "Hits",
                _ => "Greats"
            };

            goodBox.Label = ruleset.Value.ShortName switch
            {
                "osu" => "100s",
                "fruits" => "Droplets",
                _ => "Goods"
            };

            mehBox.Label = ruleset.Value.ShortName switch
            {
                "osu" => "50s",
                "fruits" => "Tiny Droplets",
                _ => "Mehs"
            };

            greatGekiContainer.ColumnDimensions = ruleset.Value.ShortName switch
            {
                "osu" or "fruits" or "taiko" =>
                    new[]
                    {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute)
                    },
                _ =>
                    new[]
                    {
                            new Dimension(),
                            new Dimension()
                    },
            };

            goodKatuContainer.ColumnDimensions = ruleset.Value.ShortName switch
            {
                "osu" or "fruits" or "taiko" =>
                    new[]
                    {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute)
                    },
                _ =>
                    new[]
                    {
                            new Dimension(),
                            new Dimension()
                    },
            };

            fixupTextBox(goodBox);
            fixupTextBox(mehBox);
        }

        private void fixupTextBox(LabelledTextBox textbox)
        {
            // This is a hack around TextBox's way of updating layout and positioning of text
            // It can only be triggered by a couple of input events and there's no way to invalidate it from the outside
            // See: https://github.com/ppy/osu-framework/blob/fd5615732033c5ea650aa5cabc8595883a2b63f5/osu.Framework/Graphics/UserInterface/TextBox.cs#L528
            textbox.TriggerEvent(new FocusEvent(new InputState(), this));
        }

        private void resetMods()
        {
            // This is temporary solution to the UX problem that people would usually want to calculate classic scores, but classic and lazer scores have different max combo
            // We append classic mod automatically so that it is immediately obvious what's going on and makes max combo same as live
            /*var classicMod = ruleset.Value.CreateInstance().CreateAllMods().SingleOrDefault(m => m is ModClassic);

            if (classicMod != null)
            {
                appliedMods.Value = new[] { classicMod };
                return;
            }*/

            appliedMods.Value = Array.Empty<Mod>();
        }

        private void resetCalculations()
        {
            createCalculators();
            resetMods();
            calculateDifficulty();
            calculatePerformance();
            populateScoreParams();
            statisticsContainer.UpdateStatisticsContainerForRuleset(ruleset.Value);
        }

        // This is to make sure combo resets when classic mod is applied
        private int previousMaxCombo;

        private void updateCombo(bool reset)
        {
            if (difficultyAttributes is null)
                return;

            missBox.MaxValue = difficultyAttributes.MaxCombo;

            comboBox.PlaceholderText = difficultyAttributes.MaxCombo.ToString();
            comboBox.MaxValue = difficultyAttributes.MaxCombo;

            if (comboBox.Value.Value > difficultyAttributes.MaxCombo ||
                missBox.Value.Value > difficultyAttributes.MaxCombo ||
                previousMaxCombo != difficultyAttributes.MaxCombo)
                reset = true;

            if (reset)
            {
                comboBox.Text = string.Empty;
                comboBox.Value.Value = difficultyAttributes.MaxCombo;
                missBox.Text = string.Empty;
            }

            previousMaxCombo = difficultyAttributes.MaxCombo;
        }

        private void loadBackground()
        {
            if (background is not null)
            {
                RemoveInternal(background, true);
            }

            if (beatmap?.BeatmapInfo?.BeatmapSet?.OnlineID is not null)
            {
                LoadComponentAsync(background = new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = 99,
                    BlurSigma = new Vector2(6),
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            Texture = textures.Get($"https://assets.ppy.sh/beatmaps/{beatmap.BeatmapInfo.BeatmapSet.OnlineID}/covers/cover.jpg"),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            FillMode = FillMode.Fill
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = OsuColour.Gray(0),
                            Alpha = 0.85f
                        },
                    }
                }).ContinueWith(_ =>
                {
                    Schedule(() =>
                    {
                        AddInternal(background);
                    });
                });
            }
        }

        private void showError(Exception e)
        {
            Logger.Log(e.ToString(), level: LogLevel.Error);

            var message = e is AggregateException aggregateException ? aggregateException.Flatten().Message : e.Message;
            showError(message, false);
        }

        private void showError(string message, bool log = true)
        {
            if (log)
                Logger.Log(message, level: LogLevel.Error);

            notificationDisplay.Display(new Notification(message));
        }

        private partial class ReplayAttributeNumberBox : LimitedLabelledNumberBox
        {
            public ReplayAttributeNumberBox(string name, string placeholder = "", int minValue = 0)
            {
                CornerRadius = 15f;
                RelativeSizeAxes = Axes.X;
                Anchor = Anchor.TopLeft;
                Label = name;
                PlaceholderText = placeholder;
                MinValue = minValue;
            }
        }

        private partial class ReplayAttributeTextBox : LabelledTextBox
        {
            public ReplayAttributeTextBox(string name, string placeholder = "")
            {
                CornerRadius = 15f;
                RelativeSizeAxes = Axes.X;
                Anchor = Anchor.TopLeft;
                Label = name;
                PlaceholderText = placeholder;
            }

            public ReplayAttributeTextBox(string name, MarginPadding margin, string placeholder = "")
            {
                CornerRadius = 15f;
                RelativeSizeAxes = Axes.X;
                Anchor = Anchor.TopLeft;
                Label = name;
                PlaceholderText = placeholder;
                Margin = margin;
            }
        }

        private partial class StatisticsContainer : FillFlowContainer
        {
            public Dictionary<HitResult, ReplayAttributeNumberBox> StatisticsContainers = new Dictionary<HitResult, ReplayAttributeNumberBox>();
            public Dictionary<HitResult, ReplayAttributeNumberBox> MaxStatisticsContainers = new Dictionary<HitResult, ReplayAttributeNumberBox>();
            public void UpdateStatisticsContainerForRuleset(RulesetInfo ruleset)
            {
                StatisticsContainers.Clear();
                MaxStatisticsContainers.Clear();

                switch (ruleset.ShortName)
                {
                    case "osu":
                        Children = new Drawable[]
                        {
                            createDoubleDisplay(
                            [
                                StatisticsContainers[HitResult.LargeTickHit] = new ReplayAttributeNumberBox("Slider Tick"),
                                MaxStatisticsContainers[HitResult.LargeTickHit] = new ReplayAttributeNumberBox("Max Slider Tick")
                            ]),
                            createDoubleDisplay(
                            [
                                StatisticsContainers[HitResult.SliderTailHit] = new ReplayAttributeNumberBox("Slider End"),
                                MaxStatisticsContainers[HitResult.SliderTailHit] = new ReplayAttributeNumberBox("Max Slider End")
                            ]),
                            createDoubleDisplay(
                            [
                                StatisticsContainers[HitResult.LargeBonus] = new ReplayAttributeNumberBox("Spinner Bonus"),
                                MaxStatisticsContainers[HitResult.LargeBonus] = new ReplayAttributeNumberBox("Max Spinner Bonus")
                            ]),
                            createDoubleDisplay(
                            [
                                StatisticsContainers[HitResult.SmallBonus] = new ReplayAttributeNumberBox("Spinner Spin"),
                                MaxStatisticsContainers[HitResult.SmallBonus] = new ReplayAttributeNumberBox("Max Spinner Spin")
                            ])
                        };

                        // Handle CL scores sliderends
                        StatisticsContainers[HitResult.SmallTickHit] = StatisticsContainers[HitResult.SliderTailHit];
                        MaxStatisticsContainers[HitResult.SmallTickHit] = MaxStatisticsContainers[HitResult.SliderTailHit];
                        return;
                }
            }

            public void ImportContainer(ScoreInfo scoreInfo)
            {
                foreach (var key in scoreInfo.Statistics.Keys)
                {
                    if (StatisticsContainers.TryGetValue(key, out var result))
                        result.Text = scoreInfo.Statistics[key].ToString();
                }

                foreach (var key in scoreInfo.MaximumStatistics.Keys)
                {
                    if (MaxStatisticsContainers.TryGetValue(key, out var result))
                        result.Text = scoreInfo.MaximumStatistics[key].ToString();
                }
            }

            public void ExportContainer(ref ScoreInfo scoreInfo)
            {
                foreach (var key in StatisticsContainers.Keys)
                {
                    scoreInfo.Statistics[key] = safeParseInt(StatisticsContainers[key].Text);
                }

                foreach (var key in MaxStatisticsContainers.Keys)
                {
                    scoreInfo.MaximumStatistics[key] = safeParseInt(MaxStatisticsContainers[key].Text);
                }

                if (scoreInfo.Mods.Any(h => h is OsuModClassic cl && cl.NoSliderHeadAccuracy.Value))
                {
                    scoreInfo.Statistics.Remove(HitResult.SliderTailHit);
                    scoreInfo.MaximumStatistics.Remove(HitResult.SliderTailHit);
                }
                else if (scoreInfo.Ruleset.ShortName == "osu")
                {
                    scoreInfo.MaximumStatistics.Remove(HitResult.SmallTickHit);
                    scoreInfo.Statistics.Remove(HitResult.SmallTickHit);
                }
            }
        }
    }
}
