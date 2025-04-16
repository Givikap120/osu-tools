// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Database;
using osu.Game.IO.Legacy;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using SharpCompress.Compressors.LZMA;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Framework.Configuration;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public class ExtendedScoreDecoder
    {
        private IBeatmap currentBeatmap;
        private Ruleset currentRuleset;

        private float beatmapOffset;

        private readonly IRulesetStore rulesets;
        private readonly BeatmapManager beatmaps;
        private readonly SettingsManager configManager;

        public ExtendedScoreDecoder(IRulesetStore rulesets, BeatmapManager beatmaps, SettingsManager configManager)
        {
            this.rulesets = rulesets;
            this.beatmaps = beatmaps;
            this.configManager = configManager;
        }

        protected Ruleset GetRuleset(int rulesetId) => rulesets.GetRuleset(rulesetId)?.CreateInstance();

        protected WorkingBeatmap GetBeatmap(string md5Hash)
        {
            if (beatmaps == null)
                return null;

            // Try to get from manager first
            var workingBeatmap = beatmaps.GetWorkingBeatmap(beatmaps.QueryBeatmap(b => b.MD5Hash == md5Hash));

            if (workingBeatmap is DummyWorkingBeatmap)
                return null;

            // Try to get from lazer path
            var lazerPath = configManager.GetBindable<string>(Settings.LazerFolderPath).Value;

            if (lazerPath == string.Empty)
                return workingBeatmap;

            // We need this beatmap (what is BeatmapInfo-only) to get hash to find full beatmap
            // This can be avoided by passing true Storage (from lazer) to BeatmapManager, but I'm too scared to let that happen
            string hash = workingBeatmap.BeatmapInfo.Hash;

            try
            {
                workingBeatmap = new FlatWorkingBeatmap(Path.Combine(lazerPath, "files", hash[..1], hash[..2], hash));

                // In case something go wrong
                workingBeatmap.BeatmapInfo.MD5Hash = md5Hash;
                workingBeatmap.BeatmapInfo.Hash = hash;
            }
            catch (Exception)
            {
            }

            return workingBeatmap;
        }

        public Score Parse(Stream stream)
        {
            var score = new Score
            {
                Replay = new Replay()
            };

            WorkingBeatmap workingBeatmap;
            ScoreRank? decodedRank = null;
            bool haveBeatmap = false;

            using (SerializationReader sr = new SerializationReader(stream))
            {
                currentRuleset = GetRuleset(sr.ReadByte());
                var scoreInfo = new ScoreInfo { Ruleset = currentRuleset.RulesetInfo };

                score.ScoreInfo = scoreInfo;

                int version = sr.ReadInt32();

                scoreInfo.IsLegacyScore = version < LegacyScoreEncoder.FIRST_LAZER_VERSION;

                // TotalScoreVersion gets initialised to LATEST_VERSION.
                // In the case where the incoming score has either an osu!stable or old lazer version, we need
                // to mark it with the correct version increment to trigger reprocessing to new standardised scoring.
                //
                // See StandardisedScoreMigrationTools.ShouldMigrateToNewStandardised().
                scoreInfo.TotalScoreVersion = version < 30000002 ? 30000001 : LegacyScoreEncoder.LATEST_VERSION;

                string beatmapHash = sr.ReadString();
                score.ScoreInfo.BeatmapHash = beatmapHash;

                workingBeatmap = GetBeatmap(beatmapHash);
                score.ScoreInfo.BeatmapInfo = null;

                if (workingBeatmap != null && workingBeatmap is not DummyWorkingBeatmap && workingBeatmap.Beatmap.HitObjects.Count > 0)
                {
                    haveBeatmap = true;

                    currentBeatmap = workingBeatmap.GetPlayableBeatmap(currentRuleset.RulesetInfo, scoreInfo.Mods);
                    scoreInfo.BeatmapInfo = currentBeatmap.BeatmapInfo;

                    // As this is baked into hitobject timing (see `LegacyBeatmapDecoder`) we also need to apply this to replay frame timing.
                    beatmapOffset = currentBeatmap.BeatmapVersion < 5 ? LegacyBeatmapDecoder.EARLY_VERSION_TIMING_OFFSET : 0;
                }

                scoreInfo.User = new APIUser { Username = sr.ReadString() };

                // MD5Hash
                score.ScoreInfo.Hash = sr.ReadString();

                scoreInfo.SetCount300(sr.ReadUInt16());
                scoreInfo.SetCount100(sr.ReadUInt16());
                scoreInfo.SetCount50(sr.ReadUInt16());
                scoreInfo.SetCountGeki(sr.ReadUInt16());
                scoreInfo.SetCountKatu(sr.ReadUInt16());
                scoreInfo.SetCountMiss(sr.ReadUInt16());

                scoreInfo.TotalScore = sr.ReadInt32();
                scoreInfo.MaxCombo = sr.ReadUInt16();

                /* score.Perfect = */
                sr.ReadBoolean();

                scoreInfo.Mods = currentRuleset.ConvertFromLegacyMods((LegacyMods)sr.ReadInt32()).ToArray();

                // lazer replays get a really high version number.
                if (version < LegacyScoreEncoder.FIRST_LAZER_VERSION)
                    scoreInfo.Mods = scoreInfo.Mods.Append(currentRuleset.CreateMod<ModClassic>()).ToArray();

                /* score.HpGraphString = */
                sr.ReadString();

                scoreInfo.Date = sr.ReadDateTime();

                byte[] compressedReplay = sr.ReadByteArray();

                if (version >= 20140721)
                    scoreInfo.LegacyOnlineID = sr.ReadInt64();
                else if (version >= 20121008)
                    scoreInfo.LegacyOnlineID = sr.ReadInt32();

                byte[] compressedScoreInfo = null;

                if (version >= 30000001)
                    compressedScoreInfo = sr.ReadByteArray();

                if (compressedReplay?.Length > 0)
                    readCompressedData(compressedReplay, reader => readLegacyReplay(score.Replay, reader));

                if (compressedScoreInfo?.Length > 0)
                {
                    readCompressedData(compressedScoreInfo, reader =>
                    {
                        LegacyReplaySoloScoreInfo readScore = JsonConvert.DeserializeObject<LegacyReplaySoloScoreInfo>(reader.ReadToEnd());

                        Debug.Assert(readScore != null);

                        score.ScoreInfo.OnlineID = readScore.OnlineID;
                        score.ScoreInfo.Statistics = readScore.Statistics;
                        score.ScoreInfo.MaximumStatistics = readScore.MaximumStatistics;
                        score.ScoreInfo.Mods = readScore.Mods.Select(m => m.ToMod(currentRuleset)).ToArray();
                        score.ScoreInfo.ClientVersion = readScore.ClientVersion;
                        decodedRank = readScore.Rank;
                        if (readScore.UserID > 1)
                            score.ScoreInfo.RealmUser.OnlineID = readScore.UserID;

                        if (readScore.TotalScoreWithoutMods is long totalScoreWithoutMods)
                            score.ScoreInfo.TotalScoreWithoutMods = totalScoreWithoutMods;
                        else
                            LegacyScoreDecoder.PopulateTotalScoreWithoutMods(score.ScoreInfo);
                    });
                }
            }

            PopulateStatistics(score.ScoreInfo, workingBeatmap);

            if (score.ScoreInfo.IsLegacyScore)
                score.ScoreInfo.LegacyTotalScore = score.ScoreInfo.TotalScore;

            if (haveBeatmap)
            {
                StandardisedScoreMigrationTools.UpdateFromLegacy(score.ScoreInfo, workingBeatmap);

                // before returning for database import, we must restore the database-sourced BeatmapInfo.
                // if not, the clone operation in GetPlayableBeatmap will cause a dereference and subsequent database exception.
                score.ScoreInfo.BeatmapInfo = workingBeatmap.BeatmapInfo;

                // Don't do this part, we want actual MD5 hash to be displayed
                // score.ScoreInfo.BeatmapHash = workingBeatmap.BeatmapInfo.Hash;
            }

            return score;
        }

        private void readCompressedData(byte[] data, Action<StreamReader> readFunc)
        {
            using (var replayInStream = new MemoryStream(data))
            {
                byte[] properties = new byte[5];
                if (replayInStream.Read(properties, 0, 5) != 5)
                    throw new IOException("input .lzma is too short");

                long outSize = 0;

                for (int i = 0; i < 8; i++)
                {
                    int v = replayInStream.ReadByte();
                    if (v < 0)
                        throw new IOException("Can't Read 1");

                    outSize |= (long)(byte)v << (8 * i);
                }

                long compressedSize = replayInStream.Length - replayInStream.Position;

                using (var lzma = new LzmaStream(properties, replayInStream, compressedSize, outSize))
                using (var reader = new StreamReader(lzma))
                    readFunc(reader);
            }
        }

        /// <summary>
        /// Populates the <see cref="ScoreInfo.MaximumStatistics"/> for a given <see cref="ScoreInfo"/>.
        /// </summary>
        /// <param name="score">The score to populate the statistics of.</param>
        /// <param name="workingBeatmap">The corresponding <see cref="WorkingBeatmap"/>.</param>
        public static void PopulateStatistics(ScoreInfo score, WorkingBeatmap workingBeatmap)
        {
            if (score.MaximumStatistics.Select(kvp => kvp.Value).Sum() > 0)
                return;

            var ruleset = score.Ruleset.Detach();
            var rulesetInstance = ruleset.CreateInstance();
            var scoreProcessor = rulesetInstance.CreateScoreProcessor();

            // Populate the maximum statistics.
            HitResult maxBasicResult = rulesetInstance.GetHitResults()
                                                      .Select(h => h.result)
                                                      .Where(h => h.IsBasic()).MaxBy(scoreProcessor.GetBaseScoreForResult);

            foreach ((HitResult result, int count) in score.Statistics)
            {
                switch (result)
                {
                    case HitResult.LargeTickHit:
                    case HitResult.LargeTickMiss:
                        score.MaximumStatistics[HitResult.LargeTickHit] = score.MaximumStatistics.GetValueOrDefault(HitResult.LargeTickHit) + count;
                        break;

                    case HitResult.SmallTickHit:
                    case HitResult.SmallTickMiss:
                        score.MaximumStatistics[HitResult.SmallTickHit] = score.MaximumStatistics.GetValueOrDefault(HitResult.SmallTickHit) + count;
                        break;

                    case HitResult.IgnoreHit:
                    case HitResult.IgnoreMiss:
                    case HitResult.SmallBonus:
                    case HitResult.LargeBonus:
                        break;

                    default:
                        score.MaximumStatistics[maxBasicResult] = score.MaximumStatistics.GetValueOrDefault(maxBasicResult) + count;
                        break;
                }
            }

            if (!score.IsLegacyScore)
                return;

            // In osu! and osu!mania, some judgements affect combo but aren't stored to scores.
            // A special hit result is used to pad out the combo value to match, based on the max combo from the difficulty attributes.
            if (workingBeatmap != null)
            {
                var calculator = rulesetInstance.CreateDifficultyCalculator(workingBeatmap);
                var attributes = calculator.Calculate(score.Mods);

                int maxComboFromStatistics = score.MaximumStatistics.Where(kvp => kvp.Key.AffectsCombo()).Select(kvp => kvp.Value).DefaultIfEmpty(0).Sum();
                if (attributes.MaxCombo > maxComboFromStatistics)
                    score.MaximumStatistics[HitResult.LegacyComboIncrease] = attributes.MaxCombo - maxComboFromStatistics;
            }
        }

        private void readLegacyReplay(Replay replay, StreamReader reader)
        {
            float lastTime = beatmapOffset;
            ReplayFrame currentFrame = null;

            string[] frames = reader.ReadToEnd().Split(',');

            for (int i = 0; i < frames.Length; i++)
            {
                string[] split = frames[i].Split('|');

                if (split.Length < 4)
                    continue;

                if (split[0] == "-12345")
                {
                    // Todo: The seed is provided in split[3], which we'll need to use at some point
                    continue;
                }

                float diff = Parsing.ParseFloat(split[0]);
                float mouseX = Parsing.ParseFloat(split[1], Parsing.MAX_COORDINATE_VALUE);
                float mouseY = Parsing.ParseFloat(split[2], Parsing.MAX_COORDINATE_VALUE);

                lastTime += diff;

                if (i < 2 && mouseX == 256 && mouseY == -500)
                    // at the start of the replay, stable places two replay frames, at time 0 and SkipBoundary - 1, respectively.
                    // both frames use a position of (256, -500).
                    // ignore these frames as they serve no real purpose (and can even mislead ruleset-specific handlers - see mania)
                    continue;

                // Todo: At some point we probably want to rewind and play back the negative-time frames
                // but for now we'll achieve equal playback to stable by skipping negative frames
                if (diff < 0)
                    continue;

                currentFrame = convertFrame(new LegacyReplayFrame(lastTime,
                    mouseX,
                    mouseY,
                    (ReplayButtonState)Parsing.ParseInt(split[3])), currentFrame);

                replay.Frames.Add(currentFrame);
            }
        }

        private ReplayFrame convertFrame(LegacyReplayFrame currentFrame, ReplayFrame lastFrame)
        {
            var convertible = currentRuleset.CreateConvertibleReplayFrame();
            if (convertible == null)
                throw new InvalidOperationException($"Legacy replay cannot be converted for the ruleset: {currentRuleset.Description}");

            convertible.FromLegacy(currentFrame, currentBeatmap, lastFrame);

            var frame = (ReplayFrame)convertible;
            frame.Time = currentFrame.Time;

            return frame;
        }

        public class BeatmapNotFoundException : Exception
        {
            public string Hash { get; }

            public BeatmapNotFoundException(string hash)
            {
                Hash = hash;
            }
        }
    }
}
