// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace PerformanceCalculatorGUI.Configuration
{
    public class ProfileCollectionManager : CollectionManager<ProfileCollection>
    {
        protected override string CollectionsDirectory => "collections_profile";
        protected override string CollectionsFilePathOld => "collection_profiles.json";
    }
}
