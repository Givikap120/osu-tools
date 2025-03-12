using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Scoring;

namespace PerformanceCalculatorGUI.Configuration
{
    public class Collection
    {
        [JsonProperty("name")]
        public Bindable<string> Name { get; private set; }

        [JsonProperty("cover_beatmapset_id")]
        public Bindable<string> CoverBeatmapSetId { get; private set; }

        [JsonProperty("scores")]
        public List<string> EncodedScores { get; private set; } = [];

        [JsonIgnore]
        public BindableList<ScoreInfo> Scores { get; private set; } = [];

        public Collection(string name, int coverBeatmapSetId)
        {
            Name = new Bindable<string>(name);
            CoverBeatmapSetId = new Bindable<string>(coverBeatmapSetId.ToString());
        }

        public Collection()
        {
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
            foreach (var score in EncodedScores)
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
    public class CollectionManager
    {
        private readonly string jsonFilePath;

        public BindableList<Collection> Collections { get; private set; }

        public Collection ActiveCollection = null;

        public CollectionManager(string jsonFile)
        {
            jsonFilePath = jsonFile;
        }

        public void Load()
        {
            if (!File.Exists(jsonFilePath))
                File.WriteAllText(jsonFilePath, "[]");

            Collections = new BindableList<Collection>(JsonConvert.DeserializeObject<List<Collection>>(File.ReadAllText(jsonFilePath)));
            foreach (var collection in Collections)
            {
                collection.DecodeScores();
            }


            if (!Collections.Any())
            {
                Collections.Add(new Collection("Test Collection", 1));
            }
        }

        public void Save()
        {
            foreach (var collection in Collections)
            {
                collection.EncodeScores();
            }

            string json = JsonConvert.SerializeObject(Collections);
            File.WriteAllText(jsonFilePath, json);
        }
    }
}
