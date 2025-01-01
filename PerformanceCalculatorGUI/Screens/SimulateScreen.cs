﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
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
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;
using osuTK;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;
using PerformanceCalculatorGUI.Screens.ObjectInspection;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class SimulateScreen : PerformanceCalculatorScreen
    {
        private ProcessorWorkingBeatmap working;

        private ExtendedUserModSelectOverlay userModsSelectOverlay;

        private GridContainer beatmapImportContainer;
        private LabelledTextBox beatmapFileTextBox;
        private LabelledTextBox beatmapIdTextBox;
        private SwitchButton beatmapImportTypeSwitch;

        private GridContainer missesContainer;
        private LimitedLabelledNumberBox missesTextBox;
        private LimitedLabelledNumberBox largeTickMissesTextBox;
        private LimitedLabelledNumberBox sliderTailMissesTextBox;
        private LimitedLabelledNumberBox comboTextBox;
        private LimitedLabelledNumberBox scoreTextBox;

        private GridContainer accuracyContainer;
        private LimitedLabelledFractionalNumberBox accuracyTextBox;
        private LimitedLabelledNumberBox goodsTextBox;
        private LimitedLabelledNumberBox mehsTextBox;
        private SwitchButton fullScoreDataSwitch;

        private DifficultyAttributes difficultyAttributes;
        private FillFlowContainer difficultyAttributesContainer;
        private FillFlowContainer performanceAttributesContainer;

        private LimitedLabelledNumberBox skillTextBox;

        private PerformanceCalculator performanceCalculator;

        [Cached]
        private Bindable<DifficultyCalculator> difficultyCalculator = new Bindable<DifficultyCalculator>();

        private FillFlowContainer beatmapDataContainer;
        private Container beatmapTitle;

        private ModDisplay modDisplay;

        private StrainVisualizer strainVisualizer;

        private ObjectInspector objectInspector;

        private BufferedContainer background;

        private ScheduledDelegate debouncedPerformanceUpdate;

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
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);
        public override bool ShouldShowConfirmationDialogOnSwitch => working != null;

        private const int file_selection_container_height = 40;
        private const int map_title_container_height = 40;
        private const float mod_selection_container_scale = 0.7f;

        private CancellationTokenSource cancellationTokenSource;
        public SimulateScreen()
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
                            beatmapImportContainer = new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Absolute),
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        beatmapFileTextBox = new FileChooserLabelledTextBox(configManager.GetBindable<string>(Settings.DefaultPath), ".osu")
                                        {
                                            Label = "Beatmap File",
                                            FixedLabelWidth = 100f,
                                            PlaceholderText = "Click to select a beatmap file"
                                        },
                                        beatmapIdTextBox = new LimitedLabelledNumberBox
                                        {
                                            Label = "Beatmap ID",
                                            FixedLabelWidth = 100f,
                                            PlaceholderText = "Enter beatmap ID",
                                            CommitOnFocusLoss = false
                                        },
                                        beatmapImportTypeSwitch = new SwitchButton
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
                                Name = "Beatmap title",
                                RelativeSizeAxes = Axes.Both
                            }
                        },
                        new Drawable[]
                        {
                            beatmapDataContainer = new FillFlowContainer
                            {
                                Name = "Beatmap data",
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
                                                    Text = "Score params"
                                                },
                                                accuracyContainer = new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    ColumnDimensions = new[]
                                                    {
                                                        new Dimension(),
                                                        new Dimension(GridSizeMode.Absolute),
                                                        new Dimension(GridSizeMode.Absolute),
                                                        new Dimension(GridSizeMode.AutoSize)
                                                    },
                                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                                    Content = new[]
                                                    {
                                                        new Drawable[]
                                                        {
                                                            accuracyTextBox = new LimitedLabelledFractionalNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Accuracy",
                                                                PlaceholderText = "100",
                                                                MaxValue = 100.0,
                                                                MinValue = 0.0,
                                                                Value = { Value = 100.0 }
                                                            },
                                                            goodsTextBox = new LimitedLabelledNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Goods",
                                                                PlaceholderText = "0",
                                                                MinValue = 0
                                                            },
                                                            mehsTextBox = new LimitedLabelledNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Mehs",
                                                                PlaceholderText = "0",
                                                                MinValue = 0
                                                            },
                                                            fullScoreDataSwitch = new SwitchButton
                                                            {
                                                                Width = 80,
                                                                Height = 40
                                                            }
                                                        }
                                                    }
                                                },
                                                comboTextBox = new LimitedLabelledNumberBox
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    Label = "Combo",
                                                    PlaceholderText = "0",
                                                    MinValue = 0
                                                },
                                                missesContainer = new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    ColumnDimensions = new[]
                                                    {
                                                        new Dimension(),
                                                        new Dimension(),
                                                        new Dimension()
                                                    },
                                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                                    Content = new[]
                                                    {
                                                        new Drawable[]
                                                        {
                                                            missesTextBox = new LimitedLabelledNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Misses",
                                                                PlaceholderText = "0",
                                                                MinValue = 0
                                                            },
                                                            largeTickMissesTextBox = new LimitedLabelledNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Large Tick Misses",
                                                                PlaceholderText = "0",
                                                                MinValue = 0
                                                            },
                                                            sliderTailMissesTextBox = new LimitedLabelledNumberBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Anchor = Anchor.TopLeft,
                                                                Label = "Slider Tail Misses",
                                                                PlaceholderText = "0",
                                                                MinValue = 0
                                                            }
                                                        }
                                                    }
                                                },
                                                scoreTextBox = new LimitedLabelledNumberBox
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    Label = "Score",
                                                    PlaceholderText = "1000000",
                                                    MinValue = 0,
                                                    MaxValue = int.MaxValue,
                                                    Value = { Value = 1000000 }
                                                },
                                                new OsuSpriteText
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    Font = new FontUsage(size: 14.0f),
                                                    Colour = osuColour.Yellow,
                                                    Text = "Don't forget to enable CL (classic) mod for osu!stable score simulation!"
                                                },
                                                new FillFlowContainer
                                                {
                                                    Name = "Test container",
                                                    Height = 40,
                                                    Direction = FillDirection.Horizontal,
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    AutoSizeAxes = Axes.Y,
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
                                                            Width = 40,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = testAR,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "AR"
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 40,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = testDT,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "RA"
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 60,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = testDTfixedAR,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "RA adj"
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 40,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = testCS,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "CS"
                                                        },
                                                        skillTextBox = new LimitedLabelledNumberBox
                                                        {
                                                            RelativeSizeAxes = Axes.None,
                                                            Width = 120,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Label = "Skill",
                                                            PlaceholderText = "1000",
                                                            MinValue = 0
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 50,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = printFCProbability,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "Test"
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 60,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
                                                            Action = exportHitData,
                                                            BackgroundColour = colourProvider.Background1,
                                                            Text = "Export"
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Width = 100,
                                                            Margin = new MarginPadding { Top = 4.0f, Right = 5.0f },
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
                                                    Margin = new MarginPadding { Left = 10f, Top = 5f, Bottom = 10.0f },
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Difficulty Attributes"
                                                },
                                                difficultyAttributesContainer = new FillFlowContainer
                                                {
                                                    Direction = FillDirection.Vertical,
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    AutoSizeAxes = Axes.Y,
                                                    Spacing = new Vector2(0, 2f)
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
                                                },
                                                new OsuSpriteText
                                                {
                                                    Margin = new MarginPadding(10.0f),
                                                    Origin = Anchor.TopLeft,
                                                    Height = 20,
                                                    Text = "Strain graph (alt+scroll to zoom)"
                                                },
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.TopLeft,
                                                    AutoSizeAxes = Axes.Y,
                                                    Child = strainVisualizer = new StrainVisualizer()
                                                },
                                                new RoundedButton
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    Width = 250,
                                                    BackgroundColour = colourProvider.Background1,
                                                    Text = "Inspect Object Difficulty Data",
                                                    Action = () =>
                                                    {
                                                        if (objectInspector is not null)
                                                            RemoveInternal(objectInspector, true);

                                                        AddInternal(objectInspector = new ObjectInspector(working)
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Size = new Vector2(0.95f)
                                                        });
                                                        objectInspector.Show();
                                                    }
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

            beatmapFileTextBox.Current.BindValueChanged(filePath => { changeBeatmap(filePath.NewValue); });
            beatmapIdTextBox.OnCommit += (_, _) => { changeBeatmap(beatmapIdTextBox.Current.Value); };

            beatmapImportTypeSwitch.Current.BindValueChanged(val =>
            {
                if (val.NewValue)
                {
                    beatmapImportContainer.ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize)
                    };
                }
                else
                {
                    beatmapImportContainer.ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize)
                    };

                    fixupTextBox(beatmapIdTextBox);
                }
            });

            accuracyTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            goodsTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            mehsTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            missesTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            largeTickMissesTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            sliderTailMissesTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            comboTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());
            scoreTextBox.Value.BindValueChanged(_ => debouncedCalculatePerformance());

            fullScoreDataSwitch.Current.BindValueChanged(val => updateAccuracyParams(val.NewValue));

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
                    calculateDifficulty().ContinueWith((t) => calculatePerformance());
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationTokenSource?.Cancel();

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
            // Hotfix for preventing a difficulty and performance calculation from being trigger twice,
            // as the mod overlay for some reason triggers a ValueChanged twice per mod change.
            if (mods.OldValue.SequenceEqual(mods.NewValue))
                return;

            modSettingChangeTracker?.Dispose();

            if (working is null)
                return;

            updateMissesTextboxes();

            modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
            modSettingChangeTracker.SettingChanged += m =>
            {
                updateMissesTextboxes();
                debouncedStatisticsUpdate?.Cancel();
                debouncedStatisticsUpdate = Scheduler.AddDelayed(() =>
                {
                    var token = resetAndGetToken();
                    calculateDifficulty(token).ContinueWith((t) => calculatePerformance(token));
                }, 100);
            };

            var token = resetAndGetToken();
            calculateDifficulty(token).ContinueWith((t) => { updateCombo(false); calculatePerformance(token); });

            void updateMissesTextboxes()
            {
                if (ruleset.Value.ShortName == "osu")
                {
                    // Large tick misses and slider tail misses are only relevant in PP if slider head accuracy exists
                    if (mods.NewValue.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value))
                    {
                        missesContainer.Content = new[] { new[] { missesTextBox } };
                        missesContainer.ColumnDimensions = [new Dimension()];
                    }
                    else
                    {
                        missesContainer.Content = new[] { new[] { missesTextBox, largeTickMissesTextBox, sliderTailMissesTextBox } };
                        missesContainer.ColumnDimensions = [new Dimension(), new Dimension(), new Dimension()];
                    }
                }
            }
        }

        private void resetBeatmap()
        {
            working = null;
            beatmapTitle.Clear();
            resetMods();
            beatmapDataContainer.Hide();

            if (background is not null)
            {
                RemoveInternal(background, true);
            }
        }

        private void changeBeatmap(string beatmap)
        {
            beatmapDataContainer.Hide();

            if (string.IsNullOrEmpty(beatmap))
            {
                showError("Empty beatmap path!");
                resetBeatmap();
                return;
            }

            try
            {
                working = ProcessorWorkingBeatmap.FromFileOrId(beatmap, audio, configManager.GetBindable<string>(Settings.CachePath).Value);
            }
            catch (Exception e)
            {
                showError(e);
                resetBeatmap();
                return;
            }

            if (working is null)
                return;

            if (!working.BeatmapInfo.Ruleset.Equals(ruleset.Value))
            {
                ruleset.Value = working.BeatmapInfo.Ruleset;
            }
            else
            {
                resetCalculations();
            }

            beatmapTitle.Add(new BeatmapCard(working));

            loadBackground();

            beatmapDataContainer.Show();
        }

        private void createCalculators()
        {
            if (working is null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();
            difficultyCalculator.Value = RulesetHelper.GetExtendedDifficultyCalculator(ruleset.Value, working);
            performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
        }

        private CancellationToken resetAndGetToken()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private Task calculateDifficulty(CancellationToken token = default)
        {
            if (working == null || difficultyCalculator.Value == null)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    difficultyAttributes = difficultyCalculator.Value.Calculate(appliedMods.Value);
                    if (token.IsCancellationRequested) return;

                    Schedule(() =>
                    {
                        difficultyAttributesContainer.Children = AttributeConversion.ToDictionary(difficultyAttributes).Select(x =>
                        new ExtendedLabelledTextBox
                        {
                            ReadOnly = true,
                            Label = x.Key.Humanize().ToLowerInvariant(),
                            Text = FormattableString.Invariant($"{x.Value:N2}")
                        }
                    ).ToArray();
                    });
                }
                catch (Exception e)
                {
                    Schedule(() =>
                    {
                        showError(e);
                        //resetBeatmap();
                    });
                    return;
                }

                if (token.IsCancellationRequested)
                    return;

                Schedule(() =>
                {
                    if (difficultyCalculator.Value is IExtendedDifficultyCalculator extendedDifficultyCalculator)
                    {
                        // StrainSkill always skips the first object
                        if (working.Beatmap?.HitObjects.Count > 1)
                            strainVisualizer.TimeUntilFirstStrain.Value = (int)working.Beatmap.HitObjects[1].StartTime;

                        strainVisualizer.Skills.Value = extendedDifficultyCalculator.GetSkills();
                    }
                    else
                        strainVisualizer.Skills.Value = Array.Empty<Skill>();
                });
            }, token);
        }

        private void debouncedCalculatePerformance()
        {
            debouncedPerformanceUpdate?.Cancel();
            debouncedPerformanceUpdate = Scheduler.AddDelayed(() => calculatePerformance(), 20);
        }

        private void calculatePerformance(CancellationToken token = default)
        {
            if (working == null || difficultyAttributes == null)
                return;

            if (token.IsCancellationRequested) return;

            int? countGood = null, countMeh = null;

            if (fullScoreDataSwitch.Current.Value)
            {
                countGood = goodsTextBox.Value.Value;
                countMeh = mehsTextBox.Value.Value;
            }

            var score = RulesetHelper.AdjustManiaScore(scoreTextBox.Value.Value, appliedMods.Value);

            try
            {
                var beatmap = working.GetPlayableBeatmap(ruleset.Value, appliedMods.Value);

                var accuracy = accuracyTextBox.Value.Value / 100.0;
                Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>();

                if (ruleset.Value.OnlineID != -1)
                {
                    // official rulesets can generate more precise hits from accuracy
                    if (appliedMods.Value.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value))
                    {
                        statistics = RulesetHelper.GenerateHitResultsForRuleset(ruleset.Value, accuracyTextBox.Value.Value / 100.0, beatmap, missesTextBox.Value.Value, countMeh, countGood,
                            null, null);
                    }
                    else
                    {
                        statistics = RulesetHelper.GenerateHitResultsForRuleset(ruleset.Value, accuracyTextBox.Value.Value / 100.0, beatmap, missesTextBox.Value.Value, countMeh, countGood,
                            largeTickMissesTextBox.Value.Value, sliderTailMissesTextBox.Value.Value);
                    }

                    accuracy = RulesetHelper.GetAccuracyForRuleset(ruleset.Value, beatmap, statistics);
                }

                var ppAttributes = performanceCalculator?.Calculate(new ScoreInfo(beatmap.BeatmapInfo, ruleset.Value)
                {
                    Accuracy = accuracy,
                    MaxCombo = comboTextBox.Value.Value,
                    Statistics = statistics,
                    Mods = appliedMods.Value.ToArray(),
                    TotalScore = score,
                    Ruleset = ruleset.Value
                }, difficultyAttributes);

                Schedule(() =>
                {
                    performanceAttributesContainer.Children = AttributeConversion.ToDictionary(ppAttributes).Select(x =>
                    new ExtendedLabelledTextBox
                    {
                        ReadOnly = true,
                        Label = x.Key.Humanize().ToLowerInvariant(),
                        Text = FormattableString.Invariant($"{x.Value:N2}")
                    }
                ).ToArray();
                });
            }
            catch (Exception e)
            {
                showError(e);
                //resetBeatmap();
            }
        }

        private void populateScoreParams()
        {
            accuracyContainer.Hide();
            comboTextBox.Hide();
            missesTextBox.Hide();
            largeTickMissesTextBox.Hide();
            sliderTailMissesTextBox.Hide();
            scoreTextBox.Hide();

            if (ruleset.Value.ShortName == "osu")
            {
                //scoreTextBox.Text = string.Empty;
                //scoreTextBox.Show();
            }
            if (ruleset.Value.ShortName == "osu" || ruleset.Value.ShortName == "taiko" || ruleset.Value.ShortName == "fruits")
            {
                updateAccuracyParams(fullScoreDataSwitch.Current.Value);
                accuracyContainer.Show();

                updateCombo(true);
                comboTextBox.Show();
                missesTextBox.Show();

                if (ruleset.Value.ShortName == "osu")
                {
                    largeTickMissesTextBox.Show();
                    sliderTailMissesTextBox.Show();
                }
            }
            else if (ruleset.Value.ShortName == "mania")
            {
                updateAccuracyParams(fullScoreDataSwitch.Current.Value);
                accuracyContainer.Show();

                missesTextBox.Show();

                scoreTextBox.Text = string.Empty;
                scoreTextBox.Show();
            }
            else
            {
                // show everything if it's something non-official
                updateAccuracyParams(false);
                accuracyContainer.Show();

                updateCombo(true);
                comboTextBox.Show();
                missesTextBox.Show();
                largeTickMissesTextBox.Show();
                sliderTailMissesTextBox.Show();

                scoreTextBox.Text = string.Empty;
                scoreTextBox.Show();
            }
        }

        private void updateAccuracyParams(bool useFullScoreData)
        {
            goodsTextBox.Text = string.Empty;
            goodsTextBox.Value.Value = 0;

            mehsTextBox.Text = string.Empty;
            mehsTextBox.Value.Value = 0;

            accuracyTextBox.Text = string.Empty;
            accuracyTextBox.Value.Value = 100;

            if (useFullScoreData)
            {
                goodsTextBox.Label = ruleset.Value.ShortName switch
                {
                    "osu" => "100s",
                    "taiko" => "Goods",
                    "fruits" => "Droplets",
                    _ => ""
                };

                mehsTextBox.Label = ruleset.Value.ShortName switch
                {
                    "osu" => "50s",
                    "fruits" => "Tiny Droplets",
                    _ => ""
                };

                accuracyContainer.ColumnDimensions = ruleset.Value.ShortName switch
                {
                    "osu" or "fruits" =>
                        new[]
                        {
                            new Dimension(GridSizeMode.Absolute),
                            new Dimension(),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize)
                        },
                    "taiko" =>
                        new[]
                        {
                            new Dimension(GridSizeMode.Absolute),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute),
                            new Dimension(GridSizeMode.AutoSize)
                        },
                    _ => new[]
                    {
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize)
                    }
                };

                fixupTextBox(goodsTextBox);
                fixupTextBox(mehsTextBox);
            }
            else
            {
                accuracyContainer.ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute),
                    new Dimension(GridSizeMode.Absolute),
                    new Dimension(GridSizeMode.AutoSize)
                };

                fixupTextBox(accuracyTextBox);
            }
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
            
            var token = resetAndGetToken();
            calculateDifficulty(token).ContinueWith((t) => { calculatePerformance(token); Schedule(() => populateScoreParams()); });
        }

        // This is to make sure combo resets when classic mod is applied
        private int previousMaxCombo;

        private void updateCombo(bool reset)
        {
            if (difficultyAttributes is null)
                return;

            missesTextBox.MaxValue = difficultyAttributes.MaxCombo;

            comboTextBox.PlaceholderText = difficultyAttributes.MaxCombo.ToString();
            comboTextBox.MaxValue = difficultyAttributes.MaxCombo;

            if (comboTextBox.Value.Value > difficultyAttributes.MaxCombo ||
                missesTextBox.Value.Value > difficultyAttributes.MaxCombo ||
                previousMaxCombo != difficultyAttributes.MaxCombo)
                reset = true;

            if (reset)
            {
                comboTextBox.Text = string.Empty;
                comboTextBox.Value.Value = difficultyAttributes.MaxCombo;
                missesTextBox.Text = string.Empty;
            }

            previousMaxCombo = difficultyAttributes.MaxCombo;
        }

        private void loadBackground()
        {
            if (background is not null)
            {
                RemoveInternal(background, true);
            }

            if (working.BeatmapInfo?.BeatmapSet?.OnlineID is not null)
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
                            Texture = textures.Get($"https://assets.ppy.sh/beatmaps/{working.BeatmapInfo.BeatmapSet.OnlineID}/covers/cover.jpg"),
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

        private (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance) calc(IReadOnlyList<Mod> mods, ScoreInfo score)
        {
            var diffAttributes = difficultyCalculator.Value.Calculate(mods);
            var ppAttributes = performanceCalculator?.Calculate(score, diffAttributes);

            return ((OsuDifficultyAttributes)diffAttributes, (OsuPerformanceAttributes)ppAttributes);
        }

        private (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance) calc(IReadOnlyList<Mod> mods)
        {
            int? countGood = null, countMeh = null;

            if (fullScoreDataSwitch.Current.Value)
            {
                countGood = goodsTextBox.Value.Value;
                countMeh = mehsTextBox.Value.Value;
            }

            var totalScore = RulesetHelper.AdjustManiaScore(scoreTextBox.Value.Value, mods);

            var beatmap = working.GetPlayableBeatmap(ruleset.Value, mods);

            var accuracy = accuracyTextBox.Value.Value / 100.0;
            Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>();

            if (ruleset.Value.OnlineID != -1)
            {
                statistics = RulesetHelper.GenerateHitResultsForRuleset(ruleset.Value, accuracyTextBox.Value.Value / 100.0, beatmap, missesTextBox.Value.Value, countMeh, countGood, largeTickMissesTextBox.Value.Value, sliderTailMissesTextBox.Value.Value);
                accuracy = RulesetHelper.GetAccuracyForRuleset(ruleset.Value, beatmap, statistics);
            }

            var score = new ScoreInfo(beatmap.BeatmapInfo, ruleset.Value)
            {
                Accuracy = accuracy,
                MaxCombo = comboTextBox.Value.Value,
                Statistics = statistics,
                Mods = [.. mods],
                TotalScore = totalScore,
                Ruleset = ruleset.Value
            };

            return calc(mods, score);
        }

        private void testAR() => AttributeTest.TestAR(appliedMods.Value, calc);

        private void testDT() => AttributeTest.TestDT(appliedMods.Value, calc);

        private void testDTfixedAR() => AttributeTest.TestDTFixedAR(appliedMods.Value, calc);

        private void testCS() => AttributeTest.TestCS(appliedMods.Value, calc);

        private List<ObjectProbablityInfo> getHitDataInfo()
        {
            var beatmap = working.GetPlayableBeatmap(ruleset.Value, appliedMods.Value);
            var extendedCalculator = (ExtendedOsuDifficultyCalculator)difficultyCalculator.Value;
            double clockRate = getClockRate(appliedMods.Value);

            var hitObjects = extendedCalculator.GetDifficultyHitObjects(beatmap, clockRate);

            Aim aim = extendedCalculator.GetSkills().OfType<Aim>().FirstOrDefault();
            FieldInfo objectStrainsProperty = typeof(Aim).GetField("ObjectStrains", BindingFlags.Instance | BindingFlags.NonPublic);
            var objectStrains = (List<double>)objectStrainsProperty.GetValue(aim);

            double skill = skillTextBox.Text == "" ? 1000: skillTextBox.Value.Value;
            var objectInfo = ScoresGenerator.GetHitProbabilityInfo(hitObjects, objectStrains, skill);
            return objectInfo;
        }

        private void printFCProbability()
        {
            double fcProbability = getHitDataInfo().Last().CumulativeFCProbability;
            Console.WriteLine($"FC probability = {fcProbability}");
        }

        private void exportHitData()
        {
            var hitData = getHitDataInfo();
            CSVExporter.ExportToCSV(hitData, $"{working.BeatmapInfo.Metadata.Title} Hit Data.csv");

            var beatmap = working.GetPlayableBeatmap(ruleset.Value, appliedMods.Value);

            var scoresGenerator = new ScoresGenerator(beatmap, appliedMods.Value, accuracyTextBox.Value.Value);
            var generatedScores = scoresGenerator.GenerateScores(hitData, 10);

            var diffAttributes = difficultyCalculator.Value.Calculate(appliedMods.Value);

            ScoresGenerator.CalculatePpForScores(generatedScores, performanceCalculator, diffAttributes);

            CSVExporter.ExportToCSV(generatedScores, $"{working.BeatmapInfo.Metadata.Title} Score Data.csv");
        }

        private double getClockRate(IEnumerable<Mod> mods)
        {
            var track = new TrackVirtual(10000);
            mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            return track.Rate;
        }
    }
}
