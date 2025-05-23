﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;

namespace PerformanceCalculatorGUI.Screens
{
    public class RecalculationUser
    {
        public string Name { get; private set; }
        private string id;
        private string[] previousNames;

        public RecalculationUser(string name)
        {
            this.Name = name.ToLowerInvariant();
        }

        public RecalculationUser(string name, int id, string[] previousNames)
        {
            this.Name = name.ToLowerInvariant();
            this.id = id.ToString().ToLowerInvariant();
            this.previousNames = previousNames.Select(name => name.ToLowerInvariant()).ToArray();
        }

        public bool IsThisUsername(string username)
        {
            username = username.ToLowerInvariant();
            if (username == Name) return true;
            if (username == id) return true;
            return previousNames.Contains(username);
        }
    }
}
