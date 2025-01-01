using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;
using osuTK;
using PerformanceCalculatorGUI.Components;
using SharpCompress.Common;

namespace PerformanceCalculatorGUI.Screens
{
    public class ScoresGenerator
    {
        private IBeatmap beatmap;
        private IReadOnlyList<Mod> mods;
        private double targetAccuracy;

        public ScoresGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods, double accuracy)
        {
            this.beatmap = beatmap;
            this.mods = mods;
            targetAccuracy = accuracy;
        }

        public static List<ObjectProbablityInfo> GetHitProbabilityInfo(DifficultyHitObject[] hitObjects, List<double> objectStrains, double skill)
        {
            double fcProbability = 1.0;
            List<ObjectProbablityInfo> objects = [];

            int j = hitObjects.Length - objectStrains.Count;
            for (int i = 0; i < objectStrains.Count; i++)
            {
                double difficulty = objectStrains[i];
                double hitProbability = SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));
                fcProbability *= hitProbability;

                var newObject = new ObjectProbablityInfo(hitObjects[j], hitProbability, fcProbability);
                objects.Add(newObject);

                j++;
            }

            return objects;
        }

        public List<SimulationScoreInfo> GenerateScores(List<ObjectProbablityInfo> objectInfo, int amount)
        {
            List<SimulationScoreInfo> scores = [];

            for (int i = 0; i < amount; i++)
            {
                scores.Add(generateScore(objectInfo));
            }

            return scores;
        }

        private SimulationScoreInfo generateScore(List<ObjectProbablityInfo> objectInfo)
        {
            Random RandomGenerator = new Random();
            int countCircleMiss = 0;
            int countSliderMiss = 0;

            int maxCombo = 0;
            int currentCombo = 0;

            foreach (var info in objectInfo)
            {
                bool isHit = RandomGenerator.NextDouble() < info.HitProbability;

                if (isHit)
                {
                    currentCombo += info.Combo;
                }
                else
                {
                    maxCombo = Math.Max(maxCombo, currentCombo);
                    currentCombo = info.Combo - 1;

                    if (info.IsSlider)
                        countSliderMiss++;
                    else
                        countCircleMiss++;
                }
            }

            maxCombo = Math.Max(maxCombo, currentCombo);
            Dictionary<HitResult, int> accuracyHitResults = RulesetHelper.GenerateOsuHitResults(targetAccuracy, beatmap, 0, null, null, null, null);
            accuracyHitResults[HitResult.Great] -= countCircleMiss + countSliderMiss;

            var fullInfoHitResults = new Dictionary<HitResult, int>(accuracyHitResults);
            fullInfoHitResults[HitResult.Miss] += countCircleMiss + countSliderMiss;

            var missingInfoHitResults = new Dictionary<HitResult, int>(accuracyHitResults);
            missingInfoHitResults[HitResult.Miss] += countCircleMiss;
            missingInfoHitResults[HitResult.Ok] += countSliderMiss;

            return new SimulationScoreInfo(createScore(fullInfoHitResults, maxCombo), createScore(missingInfoHitResults, maxCombo));
        }

        private ScoreInfo createScore(Dictionary<HitResult, int> hitResults, int maxCombo)
        {
            var ruleset = new RulesetInfo
            {
                Name = "osu!",
                ShortName = "osu",
                OnlineID = 0,
            };

            double accuracy = RulesetHelper.GetAccuracyForRuleset(ruleset, beatmap, hitResults);

            return new ScoreInfo(beatmap.BeatmapInfo, ruleset)
            {
                Accuracy = accuracy,
                MaxCombo = maxCombo,
                Statistics = hitResults,
                Mods = [.. mods],
                Ruleset = ruleset
            };
        }

        public static void CalculatePpForScores(List<SimulationScoreInfo> scores, PerformanceCalculator calculator, DifficultyAttributes attributes)
        {
            foreach (var score in scores)
            {
                double fullInfoPp = calculator.Calculate(score.FullInfoScore, attributes).Total;
                score.FullInfoScore.PP = fullInfoPp;

                double missingInfoPp = calculator.Calculate(score.MissingInfoScore, attributes).Total;
                score.MissingInfoScore.PP = missingInfoPp;
            }
        }
    }

    public struct ObjectProbablityInfo : ICSVInfo
    {
        public double HitProbability;
        public double CumulativeFCProbability;
        public bool IsSlider;
        public int Combo;

        public ObjectProbablityInfo(DifficultyHitObject hitObject, double hitProbability, double fcProbability)
        {
            HitProbability = hitProbability;
            CumulativeFCProbability = fcProbability;
            IsSlider = hitObject.BaseObject is Slider;
            Combo = getCombo(hitObject.BaseObject);

            static int getCombo(HitObject hitObject)
            {
                int combo = 0;

                if (hitObject.Judgement.MaxResult.AffectsCombo())
                    combo++;

                foreach (var nested in hitObject.NestedHitObjects)
                    combo += getCombo(nested);

                return combo;
            }
        }

        public readonly string GetCSVHeader() => "HitProbability,FCProbability,IsSlider,Combo";

        public readonly string GetCSV() => $"{HitProbability},{CumulativeFCProbability},{IsSlider},{Combo}";
    }

    public struct SimulationScoreInfo : ICSVInfo
    {
        public ScoreInfo FullInfoScore;
        public ScoreInfo MissingInfoScore;

        public SimulationScoreInfo(ScoreInfo fullInfo, ScoreInfo missingInfo)
        {
            FullInfoScore = fullInfo;
            MissingInfoScore = missingInfo;

            // WARNING: we're using Alternate mod as flag to "don't use estimators"
            FullInfoScore.Mods = [.. FullInfoScore.Mods, new OsuModAlternate()];
        }

        public readonly string GetCSV()
        {
            string hitResultInfo = $"{FullInfoScore.GetCount300()},{MissingInfoScore.GetCount300()},{FullInfoScore.GetCount100()},{MissingInfoScore.GetCount100()},{FullInfoScore.GetCount50()},{FullInfoScore.GetCountMiss()},{MissingInfoScore.GetCountMiss()}";

            static string toStringOrEmpty(double? value)
            {
                if (value == null) return "";
                return $"{value:F2}";
            }

            string otherInfo = $"{FullInfoScore.Accuracy},{MissingInfoScore.Accuracy},{FullInfoScore.MaxCombo},{toStringOrEmpty(FullInfoScore.PP)},{toStringOrEmpty(MissingInfoScore.PP)}";
            return $"{hitResultInfo},{otherInfo}";
        }

        public readonly string GetCSVHeader()
        {
            return "Count300Full,Count300Missing,Count100Full,Count100Missing,Count50,CountMissFull,CountMissMissing,AccuracyFull,AccuracyMissing,Combo,PpFull,PpMissing";
        }
    }
}
