﻿using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Users;

namespace PerformanceCalculatorGUI
{
    public class ScoreInfoCacheManager
    {
        private const int version = 20241106;

        private GameHost gameHost;
        private string lazerPath, cachePath;

        private RealmAccess realm = null;
        private bool isCacheRelevant;

        public ScoreInfoCacheManager(GameHost gameHost, string lazerPath, string cachePath)
        {
            this.gameHost = gameHost;
            this.lazerPath = lazerPath;
            this.cachePath = cachePath;

            string realmPath = Path.Combine(lazerPath, @"client.realm");
            realm = getRealmAccess();

            if (File.Exists(cachePath))
            {
                DateTime cacheLastModified = File.GetLastWriteTime(cachePath);
                DateTime realmLastModified = File.GetLastWriteTime(realmPath);

                // If cache is newer, import from cache
                if (cacheLastModified > realmLastModified)
                {
                    isCacheRelevant = true;
                }
                // If cache is older, update it with new data
                else
                {
                    isCacheRelevant = false;
                }
            }
            // If no cache exists, export fresh data
            else
            {
                isCacheRelevant = false;
            }
        }

        private RealmAccess getRealmAccess()
        {
            var storage = gameHost.GetStorage(lazerPath);
            File.Copy(Path.Combine(lazerPath, @"client.realm"), Path.Combine(lazerPath, @"client_osutools_copy.realm"), true);
            var realmAccess = new RealmAccess(storage, @"client_osutools_copy.realm");
            return realmAccess;
        }

        public List<ScoreInfo> GetScores() => isCacheRelevant ? readFromCache() : writeToCache();

        private List<ScoreInfo> readFromCache()
        {
            List<ScoreInfo>  scores = [];

            using (var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                int cacheVersion = reader.ReadInt32();
                if (cacheVersion < version)
                    return writeToCache();

                int scoreCount = reader.ReadInt32();

                for (int i = 0; i < scoreCount; i++)
                {
                    var score = readScore(reader);
                    scores.Add(score);
                }
            }

            return scores;
        }

        private List<ScoreInfo> writeToCache()
        {
            var scores = realm.Run(r => r.All<ScoreInfo>().Detach());

            using (var stream = new FileStream(cachePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(version);
                writer.Write(scores.Count);

                foreach (var score in scores)
                {
                    writeScore(writer, score);
                }
            }

            return scores;
        }

        private static ScoreInfo readScore(BinaryReader reader)
        {
            var score = new ScoreInfo();

            score.ID = new Guid(reader.ReadBytes(16));
            score.ClientVersion = nullOnDefault(reader.ReadString());
            score.BeatmapHash = reader.ReadString();
            score.Ruleset = RulesetHelper.GetRulesetFromLegacyID(reader.ReadInt32()).RulesetInfo;
            score.BeatmapHash = reader.ReadString();
            score.Hash = reader.ReadString();
            score.TotalScore = reader.ReadInt64();
            score.TotalScoreWithoutMods = reader.ReadInt64();
            score.LegacyTotalScore = reader.ReadInt64();
            score.MaxCombo = reader.ReadInt32();
            score.Accuracy = reader.ReadDouble();
            score.Date = new DateTimeOffset(reader.ReadInt64(), TimeSpan.FromMinutes(reader.ReadInt32()));
            score.Ranked = reader.ReadBoolean();
            score.OnlineID = reader.ReadInt64();
            score.LegacyOnlineID = reader.ReadInt64();

            var user = new APIUser
            {
                Id = reader.ReadInt32(),
                Username = reader.ReadString(),
                CountryCode = (CountryCode)reader.ReadInt32()
            };
            score.User = user;

            score.ModsJson = reader.ReadString();
            score.StatisticsJson = reader.ReadString();
            score.MaximumStatisticsJson = reader.ReadString();
            score.RankInt = reader.ReadInt32();
            score.Combo = reader.ReadInt32();
            score.IsLegacyScore = reader.ReadBoolean();

            score.BeatmapInfo = readBeatmap(reader);

            return score;
        }
        private void writeScore(BinaryWriter writer, ScoreInfo score)
        {
            writer.Write(score.ID.ToByteArray());
            writer.Write(score.ClientVersion ?? "");
            writer.Write(score.BeatmapHash);
            writer.Write(score.RulesetID);
            writer.Write(score.BeatmapHash);
            writer.Write(score.Hash);
            writer.Write(score.TotalScore);
            writer.Write(score.TotalScoreWithoutMods);
            writer.Write(score.LegacyTotalScore ?? 0);
            writer.Write(score.MaxCombo);
            writer.Write(score.Accuracy);
            writer.Write(score.Date.Ticks);
            writer.Write(score.Date.Offset.Minutes);
            writer.Write(score.Ranked);
            writer.Write(score.OnlineID);
            writer.Write(score.LegacyOnlineID);
            writer.Write(score.User.OnlineID);
            writer.Write(score.User.Username);
            writer.Write((int)score.User.CountryCode);
            writer.Write(score.ModsJson);
            writer.Write(score.StatisticsJson);
            writer.Write(score.MaximumStatisticsJson);
            writer.Write(score.RankInt);
            writer.Write(score.Combo);
            writer.Write(score.IsLegacyScore);

            writeBeatmap(writer, score.BeatmapInfo);
        }

        private static BeatmapInfo readBeatmap(BinaryReader reader)
        {
            bool hasBeatmap = reader.ReadBoolean();
            if (!hasBeatmap) return null;

            var beatmap = new BeatmapInfo();

            beatmap.ID = new Guid(reader.ReadBytes(16));
            beatmap.DifficultyName = reader.ReadString();
            beatmap.Ruleset = RulesetHelper.GetRulesetFromLegacyID(reader.ReadInt32()).RulesetInfo;

            beatmap.Difficulty.ApproachRate = reader.ReadSingle();
            beatmap.Difficulty.DrainRate = reader.ReadSingle();
            beatmap.Difficulty.CircleSize = reader.ReadSingle();
            beatmap.Difficulty.OverallDifficulty = reader.ReadSingle();
            beatmap.Difficulty.SliderMultiplier = reader.ReadDouble();
            beatmap.Difficulty.SliderTickRate = reader.ReadDouble();

            beatmap.Metadata.Title = reader.ReadString();
            beatmap.Metadata.TitleUnicode = nullOnDefault(reader.ReadString());
            beatmap.Metadata.Artist = reader.ReadString();
            beatmap.Metadata.ArtistUnicode = nullOnDefault(reader.ReadString());
            beatmap.Metadata.Source = reader.ReadString();
            beatmap.Metadata.Tags = reader.ReadString();
            beatmap.Metadata.PreviewTime = reader.ReadInt32();
            beatmap.Metadata.AudioFile = nullOnDefault(reader.ReadString());
            beatmap.Metadata.BackgroundFile = nullOnDefault(reader.ReadString());

            beatmap.UserSettings.Offset = reader.ReadDouble();

            beatmap.StatusInt = reader.ReadInt32();
            beatmap.OnlineID = reader.ReadInt32();
            beatmap.Length = reader.ReadDouble();
            beatmap.BPM = reader.ReadDouble();
            beatmap.Hash = reader.ReadString();
            beatmap.StarRating = reader.ReadDouble();
            beatmap.MD5Hash = reader.ReadString();
            beatmap.OnlineMD5Hash = reader.ReadString();
            beatmap.EndTimeObjectCount = reader.ReadInt32();
            beatmap.TotalObjectCount = reader.ReadInt32();
            beatmap.BeatmapVersion = reader.ReadInt32();

            return beatmap;
        }

        private static void writeBeatmap(BinaryWriter writer, BeatmapInfo beatmap)
        {
            writer.Write(beatmap != null);
            if (beatmap == null) return;

            writer.Write(beatmap.ID.ToByteArray());
            writer.Write(beatmap.DifficultyName);
            writer.Write(beatmap.Ruleset.OnlineID);

            writer.Write(beatmap.Difficulty.ApproachRate);
            writer.Write(beatmap.Difficulty.DrainRate);
            writer.Write(beatmap.Difficulty.CircleSize);
            writer.Write(beatmap.Difficulty.OverallDifficulty);
            writer.Write(beatmap.Difficulty.SliderMultiplier);
            writer.Write(beatmap.Difficulty.SliderTickRate);

            writer.Write(beatmap.Metadata.Title);
            writer.Write(beatmap.Metadata.TitleUnicode ?? "");
            writer.Write(beatmap.Metadata.Artist);
            writer.Write(beatmap.Metadata.ArtistUnicode ?? "");
            writer.Write(beatmap.Metadata.Source);
            writer.Write(beatmap.Metadata.Tags);
            writer.Write(beatmap.Metadata.PreviewTime);
            writer.Write(beatmap.Metadata.AudioFile ?? "");
            writer.Write(beatmap.Metadata.BackgroundFile ?? "");

            writer.Write(beatmap.UserSettings.Offset);

            writer.Write(beatmap.StatusInt);
            writer.Write(beatmap.OnlineID);
            writer.Write(beatmap.Length);
            writer.Write(beatmap.BPM);
            writer.Write(beatmap.Hash);
            writer.Write(beatmap.StarRating);
            writer.Write(beatmap.MD5Hash);
            writer.Write(beatmap.OnlineMD5Hash);
            writer.Write(beatmap.EndTimeObjectCount);
            writer.Write(beatmap.TotalObjectCount);
            writer.Write(beatmap.BeatmapVersion);
        }

        private static string nullOnDefault(string s) => s == "" ? null : s;
    }
}

