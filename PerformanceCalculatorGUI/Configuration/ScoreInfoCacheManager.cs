using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Users;

namespace PerformanceCalculatorGUI.Configuration
{
    public class ScoreInfoCacheManager
    {
        public const int VERSION = 1;

        public static string CacheFileName => @"scores.cache";

        private RealmAccess realm;
        private bool isCacheRelevant;

        public ScoreInfoCacheManager(GameHost gameHost, string lazerPath)
        {
            string realmPath = Path.Combine(lazerPath, @"client.realm");
            realm = RulesetHelper.GetRealmAccess(gameHost, lazerPath);

            if (File.Exists(CacheFileName))
            {
                var cacheLastModified = File.GetLastWriteTime(CacheFileName);
                var realmLastModified = File.GetLastWriteTime(realmPath);

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

        public List<ScoreInfo> GetScores() => isCacheRelevant ? readFromCache() : writeToCache();

        private List<ScoreInfo> readFromCache()
        {
            List<ScoreInfo> scores = [];

            Logger.Log("Getting score from cache...");

            using (var stream = new FileStream(CacheFileName, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                int cacheVersion = reader.ReadInt32();
                if (cacheVersion != VERSION)
                {
                    Logger.Log("Cache has wrong version");
                    return writeToCache();
                }

                int scoreCount = reader.ReadInt32();

                for (int i = 0; i < scoreCount; i++)
                {
                    var score = ReadScore(reader, VERSION);
                    scores.Add(score);
                }
            }

            return scores;
        }

        private List<ScoreInfo> writeToCache()
        {
            Logger.Log("Getting scores from realm...");
            var scores = realm.Run(r => r.All<ScoreInfo>().Detach());

            using (var stream = new FileStream(CacheFileName, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(VERSION);
                writer.Write(scores.Count);

                foreach (var score in scores)
                {
                    WriteScore(writer, score);
                }
            }

            return scores;
        }

        public static ScoreInfo ReadScore(BinaryReader reader, int version)
        {
            var score = new ScoreInfo();

            score.ID = new Guid(reader.ReadBytes(16));
            score.ClientVersion = reader.ReadString();
            score.BeatmapHash = reader.ReadString();
            score.Ruleset = RulesetHelper.GetRulesetFromLegacyID(reader.ReadInt32()).RulesetInfo;
            score.Hash = reader.ReadString();
            score.TotalScore = reader.ReadInt64();
            score.TotalScoreWithoutMods = reader.ReadInt64();
            score.LegacyTotalScore = reader.ReadInt64();
            score.PP = reader.ReadDouble();
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

            if (version < 1)
            {
                score.BeatmapInfo = readBeatmapLegacy(reader);
                return score;
            }

            score.BeatmapInfo = new BeatmapInfo
            {
                OnlineID = reader.ReadInt32()
            };

            return score;
        }
        public static void WriteScore(BinaryWriter writer, ScoreInfo score)
        {
            if (string.IsNullOrEmpty(score.StatisticsJson))
                score.StatisticsJson = JsonConvert.SerializeObject(score.Statistics);

            if (string.IsNullOrEmpty(score.MaximumStatisticsJson))
                score.MaximumStatisticsJson = JsonConvert.SerializeObject(score.MaximumStatistics);

            writer.Write(score.ID.ToByteArray());
            writer.Write(score.ClientVersion ?? "");
            writer.Write(score.BeatmapHash);
            writer.Write(score.RulesetID);
            writer.Write(score.Hash);
            writer.Write(score.TotalScore);
            writer.Write(score.TotalScoreWithoutMods);
            writer.Write(score.LegacyTotalScore ?? 0);
            writer.Write(score.PP ?? 0);
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
            writer.Write(score.BeatmapInfo?.OnlineID ?? -1);
        }

        private static BeatmapInfo? readBeatmapLegacy(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;

            var beatmap = new BeatmapInfo();

            reader.BaseStream.Seek(16, SeekOrigin.Current);
            reader.ReadString();
            reader.BaseStream.Seek(36, SeekOrigin.Current);
            for (int i = 0; i < 6; i++) reader.ReadString();
            reader.BaseStream.Seek(4, SeekOrigin.Current);
            for (int i = 0; i < 2; i++) reader.ReadString();
            reader.BaseStream.Seek(12, SeekOrigin.Current);

            beatmap.OnlineID = reader.ReadInt32();

            reader.BaseStream.Seek(16, SeekOrigin.Current);
            reader.ReadString();
            reader.BaseStream.Seek(8, SeekOrigin.Current);
            for (int i = 0; i < 2; i++) reader.ReadString();
            reader.BaseStream.Seek(8, SeekOrigin.Current);

            return beatmap;
        }

        private static string? nullOnDefault(string s) => s == "" ? null : s;
    }
}


