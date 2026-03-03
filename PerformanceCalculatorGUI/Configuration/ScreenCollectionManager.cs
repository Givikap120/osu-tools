// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace PerformanceCalculatorGUI.Configuration
{
    public class ScreenCollectionManager : CollectionManager<MyCollection>
    {
        protected override string CollectionsDirectory => "collections_custom";
        protected override string CollectionsFilePathOld => "collections.json";

        public MyCollection? ActiveCollection = null;

        public override void Load()
        {
            base.Load();

            if (Collections.Count == 0)
            {
                Collections.Add(new MyCollection("Test Collection", 1, 0));
            }
        }
    }
}
