// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTabletDriver.Native.Windows.Input;
using osu.Framework.Audio;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using PerformanceCalculatorGUI.Configuration;
using PerformanceCalculatorGUI.Screens;

namespace PerformanceCalculatorGUI.Components.BeatmapDataExport
{
    public class BeatmapDataExporter
    {
        private RealmToolsScreen parent;
        private AudioManager audioManager { get; set; } = null!;
        private SettingsManager configManager { get; set; } = null!;
        private BeatmapManager beatmapManager { get; set; } = null!;

        private string lazerPath;

        private readonly Ruleset rulesetInstance = RulesetHelper.GetRulesetFromLegacyID(0)!;
        private readonly List<Mod[]> modCombinations = getModCombinations();

        private bool combineCSV;
        private List<string> baseRows = [];
        private List<string> modRows = [];

        public BeatmapDataExporter(RealmToolsScreen parent, AudioManager audioManager, SettingsManager configManager, BeatmapManager beatmapManager)
        {
            this.parent = parent;
            this.audioManager = audioManager;
            this.configManager = configManager;
            this.beatmapManager = beatmapManager;

            lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;
        }

        public void ExportBeatmapData(IEnumerable<BeatmapInfo> beatmaps, string baseInfoFilePath, string modInfoFilePath, CancellationToken token)
        {
            combineCSV = false;
            baseRows = [$"Hash,Name,{RelevantBaseData.GetHeader()}"];
            modRows = [$"Hash,{RelevantModData.GetHeader()}"];

            calculateBeatmapData(beatmaps, token);

            if (token.IsCancellationRequested)
                return;

            File.WriteAllLines(baseInfoFilePath, baseRows);
            File.WriteAllLines(modInfoFilePath, modRows);
        }

        public void ExportBeatmapData(IEnumerable<BeatmapInfo> beatmaps, string combinedInfoFilePath, CancellationToken token)
        {
            combineCSV = true;
            baseRows = [];
            modRows = [$"Name,{RelevantBaseData.GetHeader()},{RelevantModData.GetHeader()}"];

            calculateBeatmapData(beatmaps, token);

            if (token.IsCancellationRequested)
                return;

            File.WriteAllLines(combinedInfoFilePath, modRows);
        }

        private void calculateBeatmapData(IEnumerable<BeatmapInfo> beatmaps, CancellationToken token)
        {
            int index = 1;
            int total = beatmaps.Count();

            foreach (var beatmapInfo in beatmaps)
            {
                if (token.IsCancellationRequested)
                    break;

                parent.UpdateLoadingState($"Calculating beatmap data ({index}/{total})...");
                collectDataForBeatmap(beatmapInfo, token);
                index++;
            }
        }

        private void collectDataForBeatmap(BeatmapInfo beatmap, CancellationToken token)
        {
            // Retrieve the working beatmap
            WorkingBeatmap working;
            string beatmapHash = beatmap.Hash;

            try
            {
                working = new FlatWorkingBeatmap(Path.Combine(lazerPath, "files", beatmapHash[..1], beatmapHash[..2], beatmapHash));
            }
            catch (Exception)
            {
                return;
            }

            // Calculate the base info and mod info for the beatmap, and add them to the respective lists
            RelevantBaseData baseInfo = GetBaseInfo(working, token);

            // We don't want to calculate extreme edge cases that would pollute the data
            if (baseInfo.StarRating > 13 || baseInfo.StarRating < 0.5 || baseInfo.CircleCount + baseInfo.SliderCount > 5000 || baseInfo.Length > 30 * 60) return;

            if (!combineCSV) baseRows.Add($"{beatmap.MD5Hash},{getTitle(beatmap)},{baseInfo}");

            foreach (var modCombination in modCombinations)
            {
                RelevantModData modInfo = GetModInfo(working, modCombination, token);

                if (combineCSV)
                {
                    modRows.Add($"{getTitle(beatmap)},{baseInfo},{modInfo}");
                }
                else
                {
                    modRows.Add($"{beatmap.MD5Hash},{modInfo}");
                }
            }
        }

        public RelevantBaseData GetBaseInfo(WorkingBeatmap beatmap, CancellationToken token)
        {
            OsuDifficultyAttributes attributes = calculate(beatmap, [], token);
            RelevantBaseData baseData = new RelevantBaseData(beatmap, attributes);
            return baseData;
        }

        public RelevantModData GetModInfo(WorkingBeatmap beatmap, Mod[] mods, CancellationToken token)
        {
            double starRating = calculate(beatmap, mods, token).StarRating;
            RelevantModData modData = new RelevantModData(beatmap.BeatmapInfo.Difficulty, mods, starRating);
            return modData;
        }

        private OsuDifficultyAttributes calculate(WorkingBeatmap beatmap, Mod[] mods, CancellationToken token)
        {
            var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(beatmap);
            var difficultyAttributes = difficultyCalculator.Calculate(mods, token);
            return (OsuDifficultyAttributes)difficultyAttributes;
        }

        private static List<Mod[]> getModCombinations()
        {
            List<Mod[]> modCombinations = [[]];

            // Add the basic mods:
            modCombinations.Add(new Mod[] { new OsuModEasy() });
            modCombinations.Add(new Mod[] { new OsuModHardRock() });
            modCombinations.Add(new Mod[] { new OsuModHidden() });
            modCombinations.Add(new Mod[] { new OsuModFlashlight() });
            modCombinations.Add(new Mod[] { new OsuModRelax() });
            modCombinations.Add(new Mod[] { new OsuModAutopilot() });

            // Add the most popular mod combinations:
            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModHardRock() });
            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModDoubleTime() });
            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModDoubleTime(), new OsuModHardRock() });
            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModDoubleTime(), new OsuModHardRock(), new OsuModFlashlight() });

            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModEasy() });
            modCombinations.Add(new Mod[] { new OsuModEasy(), new OsuModDoubleTime() });
            modCombinations.Add(new Mod[] { new OsuModEasy(), new OsuModDoubleTime(), new OsuModHidden() });
            modCombinations.Add(new Mod[] { new OsuModEasy(), new OsuModHalfTime(), new OsuModFlashlight() });
            modCombinations.Add(new Mod[] { new OsuModHidden(), new OsuModDoubleTime(), new OsuModRelax() });
            modCombinations.Add(new Mod[] { new OsuModDoubleTime(), new OsuModAutopilot() });

            // Base rates:
            modCombinations.Add(new Mod[] { getRateMod(0.5) });
            modCombinations.Add(new Mod[] { getRateMod(0.75) });
            modCombinations.Add(new Mod[] { getRateMod(1.25) });
            modCombinations.Add(new Mod[] { getRateMod(1.5) });
            modCombinations.Add(new Mod[] { getRateMod(1.75) });
            modCombinations.Add(new Mod[] { getRateMod(2.0) });

            // Circle size
            modCombinations.Add(new Mod[] { getDifficultyAdjust(cs: 0) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(cs: 2) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(cs: 4) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(cs: 7) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(cs: 10) });

            // Approach rate
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: -10) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 0) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 5) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 10) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 11) });

            // Approach rate + Hidden
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: -10), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 0), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 5), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 8), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 10), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 10.5f), new OsuModHidden() });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(ar: 11), new OsuModHidden() });

            // Overall difficulty
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 0) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 5) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 8) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 9) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 10) });
            modCombinations.Add(new Mod[] { getDifficultyAdjust(od: 11) });

            return modCombinations;
        }

        private static Mod getRateMod(double rate)
        {
            if (rate < 1)
            {
                var result = new OsuModHalfTime();
                result.SpeedChange.Value = rate;
                return result;
            }

            else if (rate > 1)
            {
                var result = new OsuModDoubleTime();
                result.SpeedChange.Value = rate;
                return result;
            }

            return null!;
        }

        private static Mod getDifficultyAdjust(float? cs = null, float? ar = null, float? od = null)
        {
            var result = new OsuModDifficultyAdjust();

            if (cs != null)
                result.CircleSize.Value = cs.Value;
            if (ar != null)
                result.ApproachRate.Value = ar.Value;
            if (od != null)
                result.OverallDifficulty.Value = od.Value;

            return result;
        }

        private static string getTitle(BeatmapInfo beatmap)
        {
            string input = beatmap.GetDisplayTitle();

            bool mustQuote = input.Contains(',') || input.Contains('"');

            if (!mustQuote)
                return input;

            // escape quotes by doubling them
            input = input.Replace("\"", "\"\"");

            return $"\"{input}\"";
        }
    }
}
