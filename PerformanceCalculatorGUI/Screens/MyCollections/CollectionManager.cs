// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Extensions;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public abstract class CollectionManager<T> where T : MyCollection
    {
        protected abstract string CollectionsDirectory { get; }

        public BindableList<T> Collections { get; private set; } = [];

        public CollectionManager() => Load();

        public void SaveCollection(T collection)
        {
            if (!Directory.Exists(CollectionsDirectory))
                Directory.CreateDirectory(CollectionsDirectory);

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
            File.WriteAllText(Path.Combine(CollectionsDirectory, filename), json);
        }

        public void SaveAllCollections()
        {
            if (!Directory.Exists(CollectionsDirectory))
                Directory.CreateDirectory(CollectionsDirectory);

            filterSameColletions(Collections);

            foreach (string file in Directory.EnumerateFiles(CollectionsDirectory, "*.json"))
                File.Delete(file);

            foreach (var collection in Collections)
                SaveCollection(collection);
        }

        public virtual void Load()
        {
            Collections = [.. loadCollectionList(CollectionsDirectory)];
            if (migrateOldCollections(Collections)) SaveAllCollections();
        }

        private void filterSameColletions(BindableList<T> collections)
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

        private List<T> loadCollectionList(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var result = new List<T>();

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.json"))
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

        #region legacy

        protected abstract string CollectionsFilePathOld { get; }

        private bool migrateOldCollections(BindableList<T> list)
        {
            if (!File.Exists(CollectionsFilePathOld))
                return false;

            var collectionsOld = loadCollectionListOld();

            foreach (var oldCollection in collectionsOld)
            {
                bool exists = list.Any(c => c.Name == oldCollection.Name);

                if (!exists)
                    list.Add(oldCollection);
            }

            File.Delete(CollectionsFilePathOld);
            return true;
        }

        private List<T> loadCollectionListOld()
        {
            if (!File.Exists(CollectionsFilePathOld)) return [];

            var result = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(CollectionsFilePathOld)) ?? [];
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
