// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using PerformanceCalculatorGUI.Components;

namespace PerformanceCalculatorGUI.Screens.MyCollections
{
    public interface ICanCalculateCollection
    {
        OverlayColourProvider ColourProvider { get; }
        DialogOverlay DialogOverlay { get; }
        FillFlowContainer<ExtendedProfileScore> Scores { get; }
        SwitchButton DeltaModeCheckbox { get; }
        CollectionManager Collections { get; }
        RoundedButton OverwriteValuesButton { get; }
        MyCollection? CurrentCollection { get; }

        void UpdateSorting();
        void PrepareScoresBeforeUpdate() { }
    }

    public static class CalculateCollectionExtensions
    {
        public static SwitchButton ConstructDeltaModeCheckbox(this ICanCalculateCollection target, MarginPadding margin = default)
        {
            var result = new SwitchButton
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Margin = margin,
                Current = { Value = false },
            };
            result.Current.ValueChanged += target.OnDeltaModeChanged;
            return result;
        }

        public static OsuSpriteText ConstructDeltaModeCheckboxText(this ICanCalculateCollection target, MarginPadding margin = default)
        {
            return new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Margin = margin,
                Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 14),
                UseFullGlyphHeight = false,
                Text = "Delta Mode"
            };
        }

        public static RoundedButton ConstructOverwriteValuesButton(this ICanCalculateCollection target, float height)
        {
            return new RoundedButton()
            {
                Width = 170,
                Height = height,
                BackgroundColour = target.ColourProvider.Background1,
                Text = "Overwrite pp values",
                Action = target.OnOverwriteButtonPressed
            };
        }

        public static void OnOverwriteButtonPressed(this ICanCalculateCollection target)
        {
            string overwriteTarget = target.DeltaModeCheckbox.Current.Value ? "deltas" : "values";

            target.DialogOverlay.Push(new ConfirmDialog($"Do you really want to overwrite all pp {overwriteTarget} with local {overwriteTarget}?", () =>
            {
                foreach (var drawableScore in target.Scores.Children)
                {
                    var profileScore = drawableScore.Score;
                    CollectionScore scoreInfo = (CollectionScore)profileScore.ScoreInfoSource!;
                    double livePp = scoreInfo.PP ?? 0;
                    double currentPp = profileScore.PerformanceAttributes?.Total ?? 0;

                    if (target.DeltaModeCheckbox.Current.Value)
                    {
                        scoreInfo.DeltaPercentage = livePp > 0 ? currentPp / livePp : 1.0;
                    }
                    else
                    {
                        scoreInfo.PP = currentPp;
                    }
                }

                var collection = target.CurrentCollection;
                if (collection != null) target.Collections.SaveCollection(collection);

                target.UpdateVisualPpValues();
            }));
        }

        public static void OnDeltaModeChanged(this ICanCalculateCollection target, ValueChangedEvent<bool> value)
        {
            if (value.OldValue == value.NewValue) return;
            target.OverwriteValuesButton.Text = value.NewValue ? "Overwrite pp deltas" : "Overwrite pp values";
            target.UpdateVisualPpValues();
            target.UpdateSorting();
        }

        public static void UpdateVisualPpValues(this ICanCalculateCollection target)
        {
            target.PrepareScoresBeforeUpdate();

            foreach (var drawableScore in target.Scores.Children)
            {
                CollectionScore scoreInfo = (CollectionScore)drawableScore.Score.ScoreInfoSource!;
                drawableScore.LivePP = GetLivePP(target, scoreInfo);
            }
        }

        public static double GetLivePP(this ICanCalculateCollection target, CollectionScore scoreInfo)
        {
            if (target.DeltaModeCheckbox.Current.Value)
            {
                // In delta mode we base of "expected" values instead of actual values
                return (scoreInfo.PP ?? 0) * scoreInfo.DeltaPercentage;
            }
            else
            {
                return scoreInfo.PP ?? 0;
            }
        }
    }
}
