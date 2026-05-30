// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Scoring;
using osu.Game.Utils;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.BeatmapDataExport;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class RealmToolsScreen : PerformanceCalculatorScreen
    {
        private VerboseLoadingLayer loadingLayer = null!;

        private GridContainer layout = null!;

        // Export all scores
        private StatefulButton exportAllScoresButton = null!;
        private LabelledSwitchButton clearExportFolderCheckbox = null!;
        //private LabelledSwitchButton exportOnlyFromLazerCheckbox = null!;
        private LabelledTextBox exportDirectoryNameTextBox = null!;

        // Export beatmap data
        private StatefulButton exportBeatmapDataButton = null!;

        private CancellationTokenSource? calculationCancellatonToken;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Red);

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private SettingsManager configManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        private RealmAccess? realmAccess;

        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        private const int settings_height = 40;

        public RealmToolsScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6
                },
                layout = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, 40), new Dimension(GridSizeMode.Absolute), new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Name = "Scores",
                                        Height = settings_height,
                                        RelativeSizeAxes = Axes.X,
                                        Direction = FillDirection.Horizontal,
                                        Children = new Drawable[]
                                        {
                                            exportAllScoresButton = new StatefulButton("Export all scores")
                                            {
                                                Width = 150,
                                                Action = exportAllScores
                                            },

                                            clearExportFolderCheckbox = new LabelledSwitchButton
                                            {
                                                Label = "Clear folder before export",
                                                RelativeSizeAxes = Axes.None,
                                                Width = 300,
                                            },
                                            //exportOnlyFromLazerCheckbox = new LabelledSwitchButton
                                            //{
                                            //    Label = "Skip scores from stable database",
                                            //    RelativeSizeAxes = Axes.None,
                                            //    Width = 300,
                                            //},
                                            exportDirectoryNameTextBox = new LabelledTextBox
                                            {
                                                Label = "Export Folder",
                                                Text = @"exported scores",
                                                RelativeSizeAxes = Axes.None,
                                                Width = 500,
                                            },
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        Name = "Data",
                                        Height = settings_height,
                                        RelativeSizeAxes = Axes.X,
                                        Direction = FillDirection.Horizontal,
                                        Children = new Drawable[]
                                        {
                                            exportBeatmapDataButton = new StatefulButton("Export beatmap data")
                                            {
                                                Width = 150,
                                                Action = exportBeatmapData
                                            },
                                        }
                                    }
                                }
                            }
                        },
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            clearExportFolderCheckbox.Current.Value = true;
            //exportOnlyFromLazerCheckbox.Current.Value = true;
        }

        public void UpdateLoadingState(string text)
        {
            Schedule(() => loadingLayer.Text.Value = text);
        }

        private RealmAccess? getRealmAccess()
        {
            //if (realmAccess != null)
            //    return realmAccess;

            string lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;

            if (lazerPath == string.Empty)
            {
                notificationDisplay.Display(new Notification("Please set-up path to lazer database folder in GUI settings"));
                return null;
            }

            var storage = gameHost.GetStorage(lazerPath);
            File.Copy(Path.Combine(lazerPath, @"client.realm"), Path.Combine(lazerPath, @"client_osutools_copy.realm"), true);
            realmAccess = new RealmAccess(storage, @"client_osutools_copy.realm");

            return realmAccess;
        }

        private void exportAllScores()
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            exportAllScoresButton.State.Value = ButtonState.Loading;
            calculationCancellatonToken = new CancellationTokenSource();

            var realm = getRealmAccess();
            string lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;
            if (realm == null) return;

            var token = calculationCancellatonToken.Token;
            string exportDirectoryName = exportDirectoryNameTextBox.Current.Value ?? "exported scores";

            if (clearExportFolderCheckbox.Current.Value && Directory.Exists(exportDirectoryName))
            {
                foreach (string file in Directory.GetFiles(exportDirectoryName))
                {
                    File.Delete(file);
                }

                foreach (string dir in Directory.GetDirectories(exportDirectoryName))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }

            Directory.CreateDirectory(exportDirectoryName);

            var exportStorage = gameHost.GetStorage(exportDirectoryName);

            Task.Run(() =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting all scores...");
                var scores = realm.Run(r => r.All<ScoreInfo>().Detach());

                Schedule(() => loadingLayer.Text.Value = "Filtering scores...");
                scores = scores.OrderBy(x => x.Date).ToList();
                scores = RulesetHelper.FilterDuplicateScores(scores);

                int exportedScoresCount = 0;

                foreach (var score in scores)
                {
                    Schedule(() => loadingLayer.Text.Value = $"Exporting scores ({exportedScoresCount}/{scores.Count})...");
                    exportScore(score, lazerPath, exportStorage, token);
                    exportedScoresCount++;
                }
            }, token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                    exportAllScoresButton.State.Value = ButtonState.Done;
                });
            }, token);
        }

        private static void exportScore(ScoreInfo score, string lazerPath, Storage exportStorage, CancellationToken cancellationToken)
        {
            string replayPath = Path.Combine(lazerPath, "files", score.Hash[..1], score.Hash[..2], score.Hash);

            if (!File.Exists(replayPath))
                return;

            const string file_extension = ".osr";
            const int max_filename_length = 255 - (32 + 4 + 2 + 5);

            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";
            filename = filename.GetValidFilename();

            if (filename.Length > max_filename_length - file_extension.Length)
                filename = filename.Remove(max_filename_length - file_extension.Length);

            IEnumerable<string> existingExports = exportStorage.GetFiles(string.Empty, $"{filename}*{file_extension}").Concat(exportStorage.GetDirectories(string.Empty));

            filename = NamingUtils.GetNextBestFilename(existingExports, $"{filename}{file_extension}");

            try
            {
                using (var outputStream = exportStorage.CreateFileSafely(filename))
                {
                    using (var inputStream = File.OpenRead(replayPath))
                        inputStream.CopyTo(outputStream);
                }
            }
            catch
            {
                exportStorage.Delete(filename);
                throw;
            }
        }

        private void exportBeatmapData()
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            exportBeatmapDataButton.State.Value = ButtonState.Loading;
            calculationCancellatonToken = new CancellationTokenSource();

            var realm = getRealmAccess();
            if (realm == null) return;

            var token = calculationCancellatonToken.Token;

            Task.Run(() =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting beatmaps...");
                var beatmaps = getBeatmaps(realm, 10000);

                Schedule(() => loadingLayer.Text.Value = "Calculating beatmap data...");
                var exporter = new BeatmapDataExporter(this, audio, configManager, beatmapManager);
                //exporter.ExportBeatmapData(beatmaps, "baseinfo.csv", "modinfo.csv", token);
                exporter.ExportBeatmapData(beatmaps, "combinedinfo.csv", token);

            }).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                    exportBeatmapDataButton.State.Value = ButtonState.Done;
                });
            }, token);
        }

        private IEnumerable<BeatmapInfo> getBeatmaps(RealmAccess realm, int countToSelect)
        {
            return realm.Run(r =>
            {
                var list = r.All<BeatmapInfo>().ToList(); // materialize once
                int count = list.Count;

                return Enumerable.Range(0, count)
                    .OrderBy(_ => RNG.Next())
                    .Take(countToSelect)
                    .Select(i => list[i].Detach())
                    .ToList();
            });
        }
    }
}
