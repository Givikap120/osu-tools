// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
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
}
