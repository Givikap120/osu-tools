using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace PerformanceCalculatorGUI.Configuration
{
    public class Collection
    {
        [JsonProperty("name")]
        public Bindable<string> Name { get; private set; }

        [JsonProperty("cover_beatmapset_id")]
        public Bindable<string> CoverBeatmapSetId { get; private set; }

        [JsonProperty("scores")]
        public BindableList<long> Scores { get; private set; } = new BindableList<long>();

        public Collection(string name, int coverBeatmapSetId)
        {
            Name = new Bindable<string>(name);
            CoverBeatmapSetId = new Bindable<string>(coverBeatmapSetId.ToString());
        }

        public Collection()
        {
        }
    }
    public class CollectionManager
    {
        private readonly string jsonFilePath;

        public BindableList<Collection> Collections { get; private set; }

        public CollectionManager(string jsonFile)
        {
            jsonFilePath = jsonFile;
        }

        public void Load()
        {
            if (!File.Exists(jsonFilePath))
                File.WriteAllText(jsonFilePath, "[]");

            Collections = new BindableList<Collection>(JsonConvert.DeserializeObject<List<Collection>>(File.ReadAllText(jsonFilePath)));

            if (!Collections.Any())
            {
                Collections.Add(new Collection("New Collection", 1));
                Collections[0].Scores.Add(3427873257);
                Collections[0].Scores.Add(2803336922);
            }
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(Collections);
            File.WriteAllText(jsonFilePath, json);
        }
    }
}
