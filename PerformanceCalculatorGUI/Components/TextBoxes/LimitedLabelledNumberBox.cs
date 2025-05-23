﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

namespace PerformanceCalculatorGUI.Components.TextBoxes
{
    public partial class LimitedLabelledNumberBox : LabelledNumberBox
    {
        private partial class LimitedNumberBox : OsuNumberBox
        {
            public LimitedNumberBox()
            {
                Value.BindValueChanged(v =>
                {
                    if (Text == "" && v.NewValue == 0) return;
                    Text = v.NewValue.ToString();
                });
            }

            protected override void OnUserTextAdded(string added)
            {
                base.OnUserTextAdded(added);

                string textToParse = Text;

                if (string.IsNullOrEmpty(Text))
                {
                    textToParse = PlaceholderText.ToString();
                }

                if (int.TryParse(textToParse, out int parsed))
                {
                    if (parsed >= (MinValue ?? int.MinValue) && parsed <= (MaxValue ?? int.MaxValue))
                    {
                        Value.Value = parsed;
                        return;
                    }
                }

                if (textToParse == "-" && MinValue < 0)
                {
                    Value.Value = 0;
                    return;
                }

                DeleteBy(-1);
                NotifyInputError();
            }

            protected override void OnUserTextRemoved(string removed)
            {
                string textToParse = Text;

                if (string.IsNullOrEmpty(Text))
                {
                    textToParse = "0";//PlaceholderText.ToString();
                }

                if (int.TryParse(textToParse, out int parsed))
                {
                    Value.Value = parsed;
                    return;
                }

                Value.Value = default;
            }
            protected override bool CanAddCharacter(char character)
            {
                return char.IsAsciiDigit(character) || character == '-';
            }

            public int? MaxValue { get; set; }

            public int? MinValue { get; set; }

            public Bindable<int> Value { get; } = new Bindable<int>();
        }

        protected override OsuTextBox CreateTextBox() => new LimitedNumberBox();

        public int? MaxValue
        {
            set => ((LimitedNumberBox)Component).MaxValue = value;
        }

        public int? MinValue
        {
            set => ((LimitedNumberBox)Component).MinValue = value;
        }

        public Bindable<int> Value => ((LimitedNumberBox)Component).Value;

        public bool CommitOnFocusLoss
        {
            get => Component.CommitOnFocusLost;
            set => Component.CommitOnFocusLost = value;
        }

        public LimitedLabelledNumberBox()
        {
            CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS;
        }
    }
}
