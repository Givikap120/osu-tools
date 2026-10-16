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

        public static MatchSummary Calculate(IReadOnlyList<RoomGame> games)
        {
            var result = new MatchSummary();
            var players = new Dictionary<int, MatchPlayerSummary>();

            foreach (var game in games.Where(g => g.Expired && g.Scores.Count > 0))
            {
                // One entry per player per map. Newest submission wins if duplicated.
                var scores = game.Scores.GroupBy(s => s.UserID)
                    .Select(g => g.First()).ToArray();

                double[]? values = scores.Select(s => s.Passed ? (double)s.TotalScore : 0).ToArray();
                double mean = values.Average();
                double deviation = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
                double highest = values.Max();

                for (int i = 0; i < scores.Length; i++)
                {
                    var score = scores[i];
                    if (!players.TryGetValue(score.UserID, out var player))
                        players.Add(score.UserID, player = new MatchPlayerSummary { UserId = score.UserID });

                    player.Played++;
                    player.ScoreSum += values[i];
                    player.ZSum += deviation > 0 ? (values[i] - mean) / deviation : 0;
                    if (game.HasTeams && game.TeamFor(score) is "red" or "blue")
                        player.Teams.Add(game.TeamFor(score)!);

                    // Tied first places each receive a point; all-zero maps award none.
                    if (!game.HasTeams && score.Passed && highest > 0 && values[i] == highest)
                        player.Wins++;
                }

                if (!game.HasTeams)
                {
                    result.SoloMaps++;
                    continue;
                }

                result.TeamMaps++;

                if (scores.Any(s => game.TeamFor(s) is not ("red" or "blue")) || !scores.Any(s => game.TeamFor(s) == "red") || !scores.Any(s => game.TeamFor(s) == "blue"))
                {
                    result.UnassignedTeamMaps++;
                    continue;
                }

                double red = scores.Where(s => s.Passed && game.TeamFor(s) == "red").Sum(s => (double)s.TotalScore);
                double blue = scores.Where(s => s.Passed && game.TeamFor(s) == "blue").Sum(s => (double)s.TotalScore);

                if (red > blue) result.RedPoints++;
                else if (blue > red) result.BluePoints++;
                else result.TeamDraws++;
            }

            bool duel = result.TeamMaps == 0 && players.Count == 2;

            result.Players = (duel
                    ? players.Values.OrderByDescending(p => p.Wins).ThenByDescending(p => p.AverageZ)
                    : players.Values.OrderByDescending(p => p.AverageZ).ThenByDescending(p => p.Wins))
                .ThenBy(p => p.UserId).ToList();

            return result;
        }
    }
}
