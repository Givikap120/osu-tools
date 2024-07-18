﻿#nullable enable

using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.UI;
using osuTK;

namespace PerformanceCalculatorGUI.Screens.ObjectInspection.Taiko
{
    public partial class TaikoSelectableStrongableDrawableObject : TaikoSelectableDrawableObject
    {
        private bool isStrong;
        public TaikoSelectableStrongableDrawableObject(TaikoStrongableHitObject hitObject) : base(hitObject)
        {
            isStrong = hitObject.IsStrong;
        }

        protected override Vector2 GetObjectSize()
        {
            if (isStrong)
                return new Vector2(TaikoStrongableHitObject.DEFAULT_STRONG_SIZE * TaikoPlayfield.BASE_HEIGHT);

            return new Vector2(TaikoHitObject.DEFAULT_SIZE * TaikoPlayfield.BASE_HEIGHT);
        }
    }
}
