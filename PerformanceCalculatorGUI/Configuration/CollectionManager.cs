// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Extensions;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Screens;

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

    public class ProfileCollection : MyCollection
    {
        [JsonProperty("player")]
        public Bindable<RecalculationPlayer> Player { get; private set; }

        [JsonProperty("bonus_pp")]
        public decimal BonusPp { get; set; }

        public ProfileCollection(RecalculationPlayer player, int rulesetId)
        {
            Player = new Bindable<RecalculationPlayer>(player);
            Name = new Bindable<string>(player.Name);
            CoverBeatmapSetId = new Bindable<string>();
            RulesetId = rulesetId;
        }
    }

    public class CollectionManager
    {
        private const string collections_directory = "collections_custom";
        private const string collections_profile_directory = "collections_profile";

        public BindableList<MyCollection> Collections { get; private set; } = [];
        public BindableList<ProfileCollection> CollectionProfiles { get; private set; } = [];

        public MyCollection? ActiveCollection = null;

        public CollectionManager()
        {
        }

        public void SaveCollection(MyCollection collection) => saveCollection(collection, collections_directory);
        public void SaveCollectionProfile(ProfileCollection collection) => saveCollection(collection, collections_profile_directory);

        public void SaveAllCollections() => saveCollectionList(Collections, collections_directory);
        public void SaveAllCollectionProfiles() => saveCollectionList(CollectionProfiles, collections_profile_directory);

        public void Load()
        {
            Collections = [.. loadCollectionList<MyCollection>(collections_directory)];
            CollectionProfiles = [.. loadCollectionList<ProfileCollection>(collections_profile_directory)];

            if (migrateOldCollections(Collections, collections_file_path_old)) SaveAllCollections();
            if (migrateOldCollections(CollectionProfiles, collection_profiles_file_path_old)) SaveAllCollectionProfiles();

            if (Collections.Count == 0)
            {
                Collections.Add(new MyCollection("Test Collection", 1, 0));
            }
        }

        private void filterSameColletions<T>(BindableList<T> collections) where T : MyCollection
        {
            var filtered = collections
                            .GroupBy(c => c.Name)
                            .Select(group => group
                                .OrderByDescending(c => c.Scores.Count)
                                .First())
                            .ToList();

            collections.Clear();

            foreach (var item in filtered)
                collections.Add(item);
        }

        private List<T> loadCollectionList<T>(string folderPath) where T : MyCollection
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var result = new List<T>();

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.json"))
            {
                string json = File.ReadAllText(file);
                var collection = JsonConvert.DeserializeObject<T>(json);

                if (collection == null)
                    continue;

                collection.DecodeScores();
                result.Add(collection);
            }

            return result;
        }

        private void saveCollectionList<T>(BindableList<T> collections, string folderPath) where T : MyCollection
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            filterSameColletions(collections);

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.json"))
                File.Delete(file);

            foreach (var collection in collections)
                saveCollection(collection, folderPath);
        }

        private void saveCollection<T>(T collection, string folderPath) where T : MyCollection
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (collection.Scores.Count == 0) return;

            collection.Version = ScoreInfoCacheManager.VERSION;
            collection.EncodeScores();

            // Make a safe filename from the collection name
            const string extension = ".json";
            string filename = collection.Name.Value.GetValidFilename().Replace(' ', '_');

            //IEnumerable<string> existingExports = Directory.EnumerateFiles(folderPath, $"{filename}*{extension}").Concat(Directory.EnumerateDirectories(folderPath));
            //filename = NamingUtils.GetNextBestFilename(existingExports, $"{filename}{extension}");
            filename = $"{filename}{extension}";

            string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(Path.Combine(folderPath, filename), json);
        }

        #region legacy

        private const string collections_file_path_old = "collections.json";
        private const string collection_profiles_file_path_old = "collection_profiles.json";

        private bool migrateOldCollections<T>(BindableList<T> list, string migrateFilePath) where T : MyCollection
        {
            if (!File.Exists(migrateFilePath))
                return false;

            var collectionsOld = loadCollectionListOld<T>(migrateFilePath);

            foreach (var oldCollection in collectionsOld)
            {
                bool exists = list.Any(c => c.Name == oldCollection.Name);

                if (!exists)
                    list.Add(oldCollection);
            }

            File.Delete(migrateFilePath);
            return true;
        }

        private List<T> loadCollectionListOld<T>(string filePath) where T : MyCollection
        {
            if (!File.Exists(filePath)) return [];

            var result = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(filePath)) ?? [];
            result = result.Where(c => c.EncodedScores.Count > 0).ToList();

            foreach (var collection in result)
            {
                collection.DecodeScores();
            }

            return result;
        }

        #endregion
    }
}
