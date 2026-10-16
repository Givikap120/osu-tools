// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public class RoomEventsResponse
    {
        [JsonProperty("room")] public RoomInfo Room { get; set; } = new RoomInfo();
        [JsonProperty("events")] public List<RoomEvent> Events { get; set; } = new List<RoomEvent>();
        [JsonProperty("playlist_items")] public List<RoomGame> Games { get; set; } = new List<RoomGame>();
        [JsonProperty("users")] public List<RoomUser> Users { get; set; } = new List<RoomUser>();
        [JsonProperty("beatmaps")] public List<RoomBeatmap> Beatmaps { get; set; } = new List<RoomBeatmap>();
        [JsonProperty("beatmapsets")] public List<RoomBeatmapSet> BeatmapSets { get; set; } = new List<RoomBeatmapSet>();
        [JsonProperty("first_event_id")] public long FirstEventId { get; set; }
        [JsonProperty("last_event_id")] public long LastEventId { get; set; }
        [JsonProperty("current_playlist_item_id")] public long? CurrentGameId { get; set; }
    }

    public class RoomInfo
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    }

    public class RoomEvent
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("event_type")] public string Type { get; set; } = string.Empty;
        [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
        [JsonProperty("playlist_item_id")] public long? GameId { get; set; }
        [JsonProperty("user_id")] public long? UserId { get; set; }
    }

    public class RoomGame
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("beatmap_id")] public long BeatmapId { get; set; }
        [JsonProperty("ruleset_id")] public int RulesetId { get; set; }
        [JsonProperty("expired")] public bool Expired { get; set; }
        [JsonProperty("freestyle")] public bool Freestyle { get; set; }
        [JsonProperty("played_at")] public DateTimeOffset? PlayedAt { get; set; }
        [JsonProperty("required_mods")] public List<JObject> RequiredMods { get; set; } = new List<JObject>();
        [JsonProperty("allowed_mods")] public List<JObject> AllowedMods { get; set; } = new List<JObject>();
        [JsonProperty("details")] public RoomGameDetails Details { get; set; } = new RoomGameDetails();
        [JsonProperty("scores")] public List<RoomScore> Scores { get; set; } = new List<RoomScore>();

        public bool HasTeams => Details.RoomType is "team_versus" or "tag_team_versus";
        public string? TeamFor(RoomScore score) => Details.Teams.GetValueOrDefault(score.UserId);
        public long TeamTotal(string team) => Scores.Where(s => s.Passed && TeamFor(s) == team).Sum(s => s.TotalScore);
    }

    public class RoomGameDetails
    {
        [JsonProperty("room_type")] public string RoomType { get; set; } = string.Empty;
        [JsonProperty("started_at")] public DateTimeOffset StartedAt { get; set; }
        [JsonProperty("teams", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<long, string> Teams { get; set; } = new Dictionary<long, string>();
    }

    public class RoomScore
    {
        [JsonProperty("id")] public long? Id { get; set; }
        [JsonProperty("user_id")] public long UserId { get; set; }
        [JsonProperty("beatmap_id")] public long BeatmapId { get; set; }
        [JsonProperty("ruleset_id")] public int RulesetId { get; set; }
        [JsonProperty("total_score")] public long TotalScore { get; set; }
        [JsonProperty("accuracy")] public double Accuracy { get; set; }
        [JsonProperty("max_combo")] public int MaxCombo { get; set; }
        [JsonProperty("rank")] public string Rank { get; set; } = string.Empty;
        [JsonProperty("passed")] public bool Passed { get; set; }
        [JsonProperty("mods")] public List<JObject> Mods { get; set; } = new List<JObject>();
        [JsonProperty("statistics")] public Dictionary<string, int> Statistics { get; set; } = new Dictionary<string, int>();
        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; } = new Dictionary<string, JToken>();
    }

    public class RoomUser
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    }

    public class RoomBeatmap
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("beatmapset_id")] public long BeatmapSetId { get; set; }
        [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    }

    public class RoomBeatmapSet
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("artist")] public string Artist { get; set; } = string.Empty;
        [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    }

    public class RoomHistory
    {
        public RoomInfo Room { get; private set; } = new RoomInfo();
        public long? CurrentGameId { get; private set; }
        public SortedDictionary<long, RoomEvent> Events { get; } = new SortedDictionary<long, RoomEvent>();
        public Dictionary<long, RoomGame> Games { get; } = new Dictionary<long, RoomGame>();
        public Dictionary<long, RoomUser> Users { get; } = new Dictionary<long, RoomUser>();
        public Dictionary<long, RoomBeatmap> Beatmaps { get; } = new Dictionary<long, RoomBeatmap>();
        public Dictionary<long, RoomBeatmapSet> BeatmapSets { get; } = new Dictionary<long, RoomBeatmapSet>();

        public void Merge(RoomEventsResponse page)
        {
            Room = page.Room;
            CurrentGameId = page.CurrentGameId;
            foreach (var item in page.Events) Events[item.Id] = item;
            foreach (var item in page.Games) Games.TryAdd(item.Id, item);
            foreach (var item in page.Users) Users.TryAdd(item.Id, item);
            foreach (var item in page.Beatmaps) Beatmaps.TryAdd(item.Id, item);
            foreach (var item in page.BeatmapSets) BeatmapSets.TryAdd(item.Id, item);
        }

        public string Username(long? id) => id == null ? "Unknown player" : Users.GetValueOrDefault(id.Value)?.Username ?? $"User {id}";
    }
}
