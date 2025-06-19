// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API.Requests.Responses;

namespace PerformanceCalculatorGUI.Screens
{
    public class RecalculationPlayer
    {
        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("previous_names")]
        public string[] PreviousNames { get; private set; }

        public RecalculationPlayer()
        {
        }

        public RecalculationPlayer(string name)
        {
            Name = name.ToLowerInvariant();
        }

        public RecalculationPlayer(APIUser player)
        {
            UpdateFromPlayer(player);
        }

        public void UpdateFromPlayer(APIUser player)
        {
            Name = player.Username.ToLowerInvariant();
            Id = player.Id.ToString().ToLowerInvariant();
            PreviousNames = player.PreviousUsernames.Select(name => name.ToLowerInvariant()).ToArray();
        }

        public bool IsThisUsername(string username)
        {
            username = username.ToLowerInvariant();
            if (username == Name) return true;
            if (username == Id) return true;
            return PreviousNames?.Contains(username) ?? false;
        }
    }
}
