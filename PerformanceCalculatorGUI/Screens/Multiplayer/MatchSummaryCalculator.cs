// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceCalculatorGUI.Screens.Multiplayer
{
    public sealed class MatchPlayerSummary
    {
        public int UserId;
        public int Played;
        public int Wins;
        public double ScoreSum;
        public double ZSum;
        public readonly HashSet<string> Teams = new();
        public double AverageScore => Played == 0 ? 0 : ScoreSum / Played;
        public double AverageZ => Played == 0 ? 0 : ZSum / Played;
    }

    public sealed class MatchSummary
    {
        public int RedPoints;
        public int BluePoints;
        public int TeamDraws;
        public int TeamMaps;
        public int SoloMaps;
        public int UnassignedTeamMaps;
        public List<MatchPlayerSummary> Players = new();

        public static MatchSummary Calculate(IEnumerable<RoomGameCard> games)
        {
            var result = new MatchSummary();
            var players = new Dictionary<int, MatchPlayerSummary>();

            games = games.Where(game => game.IncludeInSummary.Value);

            foreach (var game in games)
            {
                if (game.ScoreCards == null)
                    continue;

                // One submission per player per map, taking the newest.
                var cards = game.ScoreCards.Children
                    .GroupBy(card => card.Score.UserID)
                    .Select(group => group
                        .OrderByDescending(card => card.Score.EndedAt)
                        .First())
                    .ToArray();

                if (cards.Length == 0)
                    continue;

                double[] values = cards
                    .Select(card => card.Score.Passed
                        ? (double)card.CurrentTotalScore
                        : 0)
                    .ToArray();

                double mean = values.Average();
                double deviation = Math.Sqrt(
                    values.Average(value => (value - mean) * (value - mean)));
                double highest = values.Max();

                for (int i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    int userId = card.Score.UserID;

                    if (!players.TryGetValue(userId, out var player))
                    {
                        player = new MatchPlayerSummary { UserId = userId };
                        players.Add(userId, player);
                    }

                    player.Played++;
                    player.ScoreSum += values[i];
                    player.ZSum += deviation > 0
                        ? (values[i] - mean) / deviation
                        : 0;

                    if (game.HasTeams && card.Team is "red" or "blue")
                        player.Teams.Add(card.Team);

                    // Tied first places share a win; all-zero maps award none.
                    if (!game.HasTeams
                        && card.Score.Passed
                        && highest > 0
                        && values[i] == highest)
                    {
                        player.Wins++;
                    }
                }

                if (!game.HasTeams)
                {
                    result.SoloMaps++;
                    continue;
                }

                result.TeamMaps++;

                if (cards.Any(card => card.Team is not ("red" or "blue"))
                    || !cards.Any(card => card.Team == "red")
                    || !cards.Any(card => card.Team == "blue"))
                {
                    result.UnassignedTeamMaps++;
                    continue;
                }

                double red = cards
                    .Where(card => card.Score.Passed && card.Team == "red")
                    .Sum(card => (double)card.CurrentTotalScore);

                double blue = cards
                    .Where(card => card.Score.Passed && card.Team == "blue")
                    .Sum(card => (double)card.CurrentTotalScore);

                if (red > blue)
                    result.RedPoints++;
                else if (blue > red)
                    result.BluePoints++;
                else
                    result.TeamDraws++;
            }

            bool duel = result.TeamMaps == 0 && players.Count == 2;

            result.Players = (duel
                    ? players.Values
                        .OrderByDescending(player => player.Wins)
                        .ThenByDescending(player => player.AverageZ)
                    : players.Values
                        .OrderByDescending(player => player.AverageZ)
                        .ThenByDescending(player => player.Wins))
                .ThenBy(player => player.UserId)
                .ToList();

            return result;
        }
    }
}
