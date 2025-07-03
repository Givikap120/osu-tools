using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Screens;

namespace PerformanceCalculatorGUI.Configuration
{
    public class Collection
    {
        [JsonProperty("name")]
        public Bindable<string> Name { get; protected set; }

        [JsonProperty("cover_beatmapset_id")]
        public Bindable<string> CoverBeatmapSetId { get; protected set; }

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonProperty("scores")]
        public List<string> EncodedScores { get; protected set; } = [];

        [JsonIgnore]
        public BindableList<ScoreInfo> Scores { get; private set; } = [];

        public Collection()
        {
        }

        public Collection(string name, int coverBeatmapSetId, int rulesetId)
        {
            Name = new Bindable<string>(name);
            CoverBeatmapSetId = new Bindable<string>(coverBeatmapSetId.ToString());
            RulesetId = rulesetId;
        }

        public void EncodeScores()
        {
            EncodedScores.Clear();
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
                ScoreInfo decodedScore = decodeScore(score);
                Scores.Add(decodedScore);
            }
        }

        private static string encodeScore(ScoreInfo score)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                ScoreInfoCacheManager.WriteScore(writer, score);
                return Convert.ToBase64String(memoryStream.ToArray()); // Convert to string
            }
        }

        private static ScoreInfo decodeScore(string data)
        {
            var byteArray = Convert.FromBase64String(data); // Convert string back to bytes
            using (var memoryStream = new MemoryStream(byteArray))
            using (var reader = new BinaryReader(memoryStream))
            {
                return ScoreInfoCacheManager.ReadScore(reader);
            }
        }
    }

    public class ProfileCollection : Collection
    {
        [JsonProperty("player")]
        public Bindable<RecalculationPlayer> Player { get; private set; }

        [JsonProperty("bonus_pp")]
        public decimal BonusPp { get; set; }

        public ProfileCollection(RecalculationPlayer player, int rulesetId)
        {
            if (player == null) return;

            Player = new Bindable<RecalculationPlayer>(player);
            Name = new Bindable<string>(player.Name);
            CoverBeatmapSetId = new Bindable<string>();
            RulesetId = rulesetId;
        }
    }

    public class CollectionManager
    {
        private const string collections_file_path = "collections.json";
        private const string collection_profiles_file_path = "collection_profiles.json";

        public BindableList<Collection> Collections { get; private set; }
        public BindableList<ProfileCollection> CollectionProfiles { get; private set; }

        public Collection ActiveCollection = null;

        public CollectionManager()
        {
        }

        private List<T> loadCollectionList<T>(string filePath) where T : Collection
        {
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, "[]");

            var result = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(filePath));
            foreach (var collection in result)
            {
                collection.DecodeScores();
            }

            return result;
        }

        public void Load()
        {
            Collections = [.. loadCollectionList<Collection>(collections_file_path)];
            CollectionProfiles = [.. loadCollectionList<ProfileCollection>(collection_profiles_file_path)];

            if (Collections.Count == 0)
            {
                Collections.Add(new Collection("Test Collection", 1, 0));
            }
        }

        private void save<T>(BindableList<T> collections, string filePath) where T : Collection
        {
            foreach (var collection in collections)
            {
                collection.EncodeScores();
            }

            string json = JsonConvert.SerializeObject(collections);
            File.WriteAllText(filePath, json);
        }

        public void SaveCollections() => save(Collections, collections_file_path);
        public void SaveCollectionProfiles() => save(CollectionProfiles, collection_profiles_file_path);

    }
}
