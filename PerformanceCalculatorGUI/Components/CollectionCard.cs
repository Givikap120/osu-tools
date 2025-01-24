// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK.Input;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public partial class CollectionCard : OsuClickableContainer, IHasPopover
    {
        private CancellationTokenSource cancellationToken;
        private Sprite backgroundSprite;

        public Collection Collection { get; }

        [Resolved]
        private LargeTextureStore textures { get; set; }

        public CollectionCard(Collection collection = null)
            : base(HoverSampleSet.Button)
        {
            Collection = collection;
            Width = 260;
            Height = 130;
            Margin = new MarginPadding(20) { Vertical = 15 };
            CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS;
        }

        public Popover GetPopover() => new CollectionPopover(Collection);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Right && Collection != null)
                this.ShowPopover();

            return base.OnMouseDown(e);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Masking = true;
            BorderColour = colours.GreyVioletLighter;

            OsuSpriteText nameText;
            AddRange(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray1
                },
                new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.3f,
                    Children = new Drawable[]
                    {
                        backgroundSprite = new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            FillMode = FillMode.Fill
                        }
                    }
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    MaxWidth = Width * 0.9f,
                    Font = OsuFont.GetFont(size: Collection == null ? 32 : 24, weight: FontWeight.Bold),
                    AllowMultiline = true
                }
            });


            if (Collection != null)
            {
                nameText.Current = Collection.Name;

                updateBackgroundTexture();

                Collection.CoverBeatmapSetId.ValueChanged += _ => updateBackgroundTexture();
            }
            else
                nameText.Text = "+";
        }

        private void updateBackgroundTexture()
        {
            cancellationToken?.Cancel();
            cancellationToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                Texture texture = await textures.GetAsync($"https://assets.ppy.sh/beatmaps/{Collection.CoverBeatmapSetId}/covers/cover.jpg", cancellationToken.Token);
                if (cancellationToken.IsCancellationRequested)
                    return;

                Schedule(() => backgroundSprite.Texture = texture);
            }, cancellationToken.Token);
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            cancellationToken?.Cancel();
            cancellationToken?.Dispose();
        }
    }
}
