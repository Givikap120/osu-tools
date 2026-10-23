// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public static class StableRoomConverter
    {
        public static RoomEventsResponse Convert(JObject response)
        {
            var match = response["match"] ?? throw new InvalidOperationException("Missing match data.");

            var result = new RoomEventsResponse
            {
                Room = new RoomInfo
                {
                    Id = match.Value<long>("id"),
                    Name = match.Value<string>("name") ?? string.Empty,
                    Type = "head_to_head"
                },
                Users = response["users"]?.ToObject<List<RoomUser>>() ?? new List<RoomUser>(),
                FirstEventId = response.Value<long>("first_event_id"),
                LastEventId = response.Value<long>("latest_event_id"),
                CurrentGameId = response.Value<long?>("current_game_id")
            };

            foreach (var entry in response["events"]?.OfType<JObject>()
                                  ?? Enumerable.Empty<JObject>())
            {
                var game = entry["game"] as JObject;
                string type = (string?)entry["detail"]?["type"] ?? "other";

                result.Events.Add(new RoomEvent
                {
                    Id = entry.Value<long>("id"),
                    CreatedAt = entry["timestamp"]!.ToObject<DateTimeOffset>(),
                    UserId = entry.Value<long?>("user_id"),
                    GameId = game?.Value<long>("id"),
                    Type = type switch
                    {
                        "other" when game != null => "game_started",
                        "match-created" => "room_created",
                        "match-disbanded" => "room_disbanded",
                        "host-changed" => "host_changed",
                        "player-joined" => "player_joined",
                        "player-left" => "player_left",
                        "player-kicked" => "player_kicked",
                        _ => "unknown"
                    }
                });

                if (game == null)
                    continue;

                var scores = game["scores"]?.ToObject<List<SoloScoreInfo>>() ?? new List<SoloScoreInfo>();

                var teams = new Dictionary<long, string>();

                foreach (var score in game["scores"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    string? team = (string?)score["match"]?["team"];

                    if (team is "red" or "blue")
                        teams[score.Value<long>("user_id")] = team;
                }

                DateTimeOffset? endTime = game["end_time"]?.ToObject<DateTimeOffset?>();

                result.Games.Add(new RoomGame
                {
                    Id = game.Value<long>("id"),
                    BeatmapId = game.Value<long>("beatmap_id"),
                    RulesetId = game.Value<int>("mode_int"),
                    Expired = endTime.HasValue,
                    PlayedAt = endTime,
                    LegacyScoringType = game.Value<string>("scoring_type"),
                    RequiredMods = (game["mods"]?.Values<string>()
                                    ?? Enumerable.Empty<string>())
                        .Select(mod => new JObject { ["acronym"] = mod })
                        .ToList(),
                    Details = new RoomGameDetails
                    {
                        StartedAt = game["start_time"]!.ToObject<DateTimeOffset?>(),
                        RoomType = game.Value<string>("team_type") switch
                        {
                            "team-vs" => "team_versus",
                            "tag-team-vs" => "tag_team_versus",
                            "tag-coop" => "tag_coop",
                            _ => "head_to_head"
                        },
                        Teams = teams
                    },
                    Scores = scores
                });

                if (game["beatmap"] is JObject beatmap)
                {
                    result.Beatmaps.Add(beatmap.ToObject<RoomBeatmap>()!);

                    if (beatmap["beatmapset"] is JObject beatmapSet)
                        result.BeatmapSets.Add(beatmapSet.ToObject<RoomBeatmapSet>()!);
                }
            }

            return result;
        }
    }
}
