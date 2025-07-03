using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Utils;
using osu.Game.Scoring.Legacy;

namespace PerformanceCalculatorGUI.Utils
{
    public static class HistoricalScoreExporter
    {
        public static void OutputHistoricalScoresCSV(List<(ScoreInfo score, WorkingBeatmap beatmap, DifficultyAttributes attributes)> allScores, Ruleset rulesetInstance, string username)
        {
            // Ensure Dot as a separator
            var customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = customCulture;

            allScores = allScores.OrderBy(x => x.score.Date).ToList();
            allScores = RulesetHelper.FilterDuplicateScores(allScores, s => s.score);

            HashSet<string> allMapHashes = [];
            HashSet<string> topPlayMapHashes = [];
            var topPlays = new SortedList<ScoreInfo>((a, b) => a.PP < b.PP ? 1 : a.PP > b.PP ? -1 : 0);

            int currentTopPlaysCount = 0;
            int uniquePlaysCount = 0;

            List<ScoreCSVInfo> csvContent = [];

            double profilePp;

            const int max_scores_in_profile = 1000;

            foreach (var score in allScores)
            {
                double pp = score.score.PP ?? 0;
                string hash = score.score.BeatmapHash;

                if (!allMapHashes.Contains(hash))
                {
                    uniquePlaysCount++;
                    allMapHashes.Add(hash);
                }

                if (currentTopPlaysCount > max_scores_in_profile && pp < topPlays[max_scores_in_profile].PP) continue;

                int position = -1;

                if (topPlayMapHashes.Contains(hash))
                {
                    var item = topPlays.Find(s => s.BeatmapHash == hash);
                    if (item.PP < pp)
                    {
                        topPlays.Remove(item);
                        position = topPlays.Add(score.score) + 1;
                    }
                }
                else
                {
                    currentTopPlaysCount++;
                    position = topPlays.Add(score.score) + 1;
                    topPlayMapHashes.Add(hash);
                }

                profilePp = 0;
                for (var i = 0; i < Math.Min(200, topPlays.Count); i++)
                    profilePp += Math.Pow(0.95, i) * (topPlays[i].PP ?? 0);

                profilePp += (417.0 - 1.0 / 3.0) * (1 - Math.Pow(0.995, Math.Min(uniquePlaysCount, 1000)));

                csvContent.Add(new ScoreCSVInfo(score.score, score.beatmap, score.attributes, rulesetInstance, profilePp, position));
            }

            string filepath = $"historical_scores_{username}.csv";
            CSVExporter.ExportToCSV(csvContent, filepath);
        }
    }

    public struct ScoreCSVInfo : ICSVInfo
    {
        public ScoreInfo Score;
        public BeatmapInfo BeatmapInfo;
        public IBeatmap Beatmap;
        public DifficultyAttributes Attributes;
        public Ruleset Ruleset;
        public double ProfilePP;
        public int Position;

        public ScoreCSVInfo(ScoreInfo score, WorkingBeatmap working, DifficultyAttributes attributes, Ruleset rulesetInstance,  double profilePP, int position)
        {
            Score = score;
            Attributes = attributes;
            BeatmapInfo = working.BeatmapInfo;
            Beatmap = working.Beatmap;
            Attributes = attributes;
            Ruleset = rulesetInstance;
            ProfilePP = profilePP;
            Position = position;
        }

        public readonly string GetCSV()
        {
            long scoreID = Score.IsLegacyScore ? Score.LegacyOnlineID : Score.OnlineID;

            string basicScoreInfo = $"{scoreID},{Score.Date},{Score.PP:F2},{ProfilePP:F0},{Position},{Score.IsLegacyScore}";
            string beatmapInfo = $"{BeatmapInfo.OnlineID},{BeatmapInfo.Metadata.Title},{BeatmapInfo.Metadata.Artist},{BeatmapInfo.DifficultyName}";

            string modsString = string.Join("", Score.APIMods.Select(m => m.Acronym));
            double rate = ModUtils.CalculateRateWithMods(Score.Mods);
            double bpm = rate * 60000 / Beatmap.GetMostCommonBeatLength();
            double length = Beatmap.CalculatePlayableLength() * 0.001 / rate;
            double starRating = Attributes.StarRating;

            var originalDifficulty = new BeatmapDifficulty(BeatmapInfo.Difficulty);

            foreach (var mod in Score.Mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(originalDifficulty);

            var adjustedDifficulty = Ruleset.GetRateAdjustedDisplayDifficulty(originalDifficulty, rate);

            string modInfo = $"{modsString},{rate:F2},{bpm:F0},{length:F0},{starRating:F2},{adjustedDifficulty.CircleSize:F1},{adjustedDifficulty.DrainRate:F1},{adjustedDifficulty.OverallDifficulty:F1},{adjustedDifficulty.ApproachRate:F1}";

            int sliderbreaks = -1;
            int sliderendmiss = -1;
            if (Attributes is OsuDifficultyAttributes osuAttributes && !Score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value))
            {
                sliderendmiss = osuAttributes.SliderCount - Score.Statistics.GetValueOrDefault(HitResult.SliderTailHit);
                sliderbreaks = Score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss);
            }
            string sliderbreaksString = sliderbreaks >= 0 ? sliderbreaks.ToString() : "";
            string sliderendmissString = sliderendmiss >= 0 ? sliderbreaks.ToString() : "";

            string advancedScoreInfo = $"{Score.Accuracy:F4},{Score.MaxCombo},{Score.GetCount300()},{Score.GetCount100()},{Score.GetCount50()},{Score.GetCountMiss()},{sliderbreaksString},{sliderendmissString}";

            return $"{basicScoreInfo},{beatmapInfo},{modInfo},{advancedScoreInfo}";
        }

        public readonly string GetCSVHeader()
        {
            return "ScoreID,Date,pp,Profile pp,Position,Legacy score,BeatmapID,Title,Artist,Difficulty Name,Mods,Rate,BPM,Length,Star Rating,CS,HP,OD,AR,Accuracy,Combo,300s,100s,50s,Misses,Sliderbreaks,Sliderend Misses";
        }
    }
}
