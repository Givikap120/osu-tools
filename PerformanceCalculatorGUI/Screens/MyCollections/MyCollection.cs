// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Screens.MyCollections;

namespace PerformanceCalculatorGUI.Configuration
{
    public class MyCollection
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("name")]
        public Bindable<string> Name { get; protected set; } = new Bindable<string>();

        [JsonProperty("cover_beatmapset_id")]
        public Bindable<string> CoverBeatmapSetId { get; protected set; } = new Bindable<string>();

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonProperty("scores")]
        public List<string> EncodedScores { get; protected set; } = [];

        [JsonIgnore]
        public BindableList<CollectionScore> Scores { get; private set; } = [];

        public MyCollection()
        {
        }

        public MyCollection(string name, int coverBeatmapSetId, int rulesetId)
        {
            Name.Value = name;
            CoverBeatmapSetId.Value = coverBeatmapSetId.ToString();
            RulesetId = rulesetId;
        }

        public void EncodeScores()
        {
            EncodedScores.Clear();
            Version = ScoreInfoCacheManager.VERSION;

            foreach (var score in Scores)
            {
                string encodedScore = encodeScore(score);
                EncodedScores.Add(encodedScore);
            }
        }

        public void DecodeScores()
        {
            Scores.Clear();

            foreach (string score in EncodedScores)
            {
                CollectionScore decodedScore = decodeScore(score, Version);
                Scores.Add(decodedScore);
            }
        }

        private static string encodeScore(CollectionScore score)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                ScoreInfoCacheManager.WriteScore(writer, score.ScoreInfo);

                writer.Write(score.MasterPp);
                writer.Write(score.BranchPp);
                writer.Write(score.DeltaPp);

                return Convert.ToBase64String(memoryStream.ToArray()); // Convert to string
            }
        }

        private static CollectionScore decodeScore(string data, int version)
        {
            byte[] byteArray = Convert.FromBase64String(data); // Convert string back to bytes
            using (var memoryStream = new MemoryStream(byteArray))
            using (var reader = new BinaryReader(memoryStream))
            {
                ScoreInfo scoreInfo = ScoreInfoCacheManager.ReadScore(reader, version);
                CollectionScore collectionScore = new CollectionScore(scoreInfo);
                if (version < 1) return collectionScore;

                collectionScore.MasterPp = reader.ReadDouble();
                collectionScore.BranchPp = reader.ReadDouble();
                collectionScore.DeltaPp = reader.ReadDouble();

                return collectionScore;
            }
        }
    }
}
