// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK.Graphics;

namespace PerformanceCalculatorGUI.Components
{
    public partial class ColorChangingButton : RoundedButton
    {
        [Resolved]
        private OverlayColourProvider colourProvider { get; set; }

        [Resolved]
        public OsuColour Colours { get; private set; }

        public ColorChangingButton()
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Background.Colour = colourProvider.Background1;
        }

        public void ChangeColor(Color4? color)
        {
            BackgroundColour = color ?? colourProvider.Background1;
            Background.FadeColour(color ?? colourProvider.Background1, 500, Easing.InOutExpo);
        }
    }
}
