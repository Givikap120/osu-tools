﻿#nullable enable

using osu.Framework.Allocation;
using osu.Game.Rulesets.Catch.Objects.Drawables;
using osu.Game.Rulesets.Catch.Objects;
using osuTK;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components;
using PerformanceCalculatorGUI.Screens.ObjectInspection.Taiko;
using System;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace PerformanceCalculatorGUI.Screens.ObjectInspection.Catch
{
    public partial class CatchSelectableHitObject : DrawableCatchHitObject
    {
        // This is HitCirclePiece instead of FruitOutline because FruitOutline doesn't register input for some reason
        private HitCirclePiece outline;
        public CatchSelectableHitObject()
            : base(new CatchDummyHitObject())
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(outline = new HitCirclePiece());
            UpdateState();
        }

        public void UpdateFromHitObject(CatchHitObject hitObject)
        {
            Deselect();
            HitObject.StartTime = hitObject.StartTime;
            X = hitObject.EffectiveX;
            outline.Scale = new Vector2(hitObject.Scale);

            if (hitObject is Droplet)
                outline.Scale *= 0.5f;
        }

        protected override void OnApply()
        {
            base.OnApply();
            UpdateState();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Right)
                return false;

            if (!IsHovered)
                return false;

            if (IsSelected)
            {
                Deselect();
                Selected.Invoke(null);
                return true;
            }

            Select();
            return true;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => outline.ReceivePositionalInputAt(screenSpacePos);

        private class CatchDummyHitObject : CatchHitObject
        {
            public CatchDummyHitObject()
            {
            }
        }

        #region Selection Logic
        public override bool HandlePositionalInput => ShouldBeAlive || IsPresent;

        private SelectionState state;

        public SelectionState State
        {
            get => state;
            set
            {
                if (state == value)
                    return;

                state = value;

                if (IsLoaded)
                    UpdateState();
            }
        }

        public void UpdateState()
        {
            switch (state)
            {
                case SelectionState.Selected:
                    OnSelected();
                    break;

                case SelectionState.NotSelected:
                    OnDeselected();
                    break;
            }
        }

        protected void OnDeselected()
        {
            foreach (var d in InternalChildren)
                d.Hide();
        }

        protected void OnSelected()
        {
            foreach (var d in InternalChildren)
                d.Show();
            Selected.Invoke(this);
        }

        public event Action<CatchSelectableHitObject?> Selected;

        public void Select() => State = SelectionState.Selected;
        public void Deselect() => State = SelectionState.NotSelected;
        public bool IsSelected => State == SelectionState.Selected;

        #endregion
    }
}
