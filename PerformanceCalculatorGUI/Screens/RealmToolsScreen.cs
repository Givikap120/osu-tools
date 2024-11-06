// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Scoring;
using osu.Game.Utils;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class RealmToolsScreen : PerformanceCalculatorScreen
    {
        private VerboseLoadingLayer loadingLayer;

        private GridContainer layout;

        // Export all scores
        private StatefulButton exportAllScoresButton;
        private LabelledSwitchButton clearExportFolderCheckbox;
        private LabelledSwitchButton exportOnlyFromLazerCheckbox;
        private LabelledTextBox exportDirectoryNameTextBox;

        private CancellationTokenSource calculationCancellatonToken;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Red);

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        //[Resolved]
        //private APIManager apiManager { get; set; }

        //[Resolved]
        //private Bindable<RulesetInfo> ruleset { get; set; }

        //[Resolved]
        //private RulesetStore rulesets { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        [Resolved]
        private GameHost gameHost { get; set; }

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
                                Name = "Settings",
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
                                    exportOnlyFromLazerCheckbox = new LabelledSwitchButton
                                    {
                                        Label = "Skip scores from stable database",
                                        RelativeSizeAxes = Axes.None,
                                        Width = 300,
                                    },
                                    exportDirectoryNameTextBox = new LabelledTextBox
                                    {
                                        Label = "Export Folder",
                                        Text = @"exported scores",
                                        RelativeSizeAxes = Axes.None,
                                        Width = 500,
                                    },
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
            exportOnlyFromLazerCheckbox.Current.Value = true;
        }

        private void exportAllScores()
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            exportAllScoresButton.State.Value = ButtonState.Loading;

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            var lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;

            if (lazerPath == string.Empty)
            {
                notificationDisplay.Display(new Notification("Please set-up path to lazer database folder in GUI settings"));
                return;
            }

            var storage = gameHost.GetStorage(lazerPath);
            File.Copy(Path.Combine(lazerPath, @"client.realm"), Path.Combine(lazerPath, @"client_osutools_copy.realm"), true);
            var realm = new RealmAccess(storage, @"client_osutools_copy.realm");

            string exportDirectoryName = exportDirectoryNameTextBox.Current.Value ?? "exported scores";

            if (clearExportFolderCheckbox.Current.Value && Directory.Exists(exportDirectoryName))
            {
                foreach (var file in Directory.GetFiles(exportDirectoryName))
                {
                    File.Delete(file);
                }

                foreach (var dir in Directory.GetDirectories(exportDirectoryName))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }

            Directory.CreateDirectory(exportDirectoryName);

            var exportStorage = gameHost.GetStorage(exportDirectoryName);

            Task.Run(async () =>
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
    }
}
