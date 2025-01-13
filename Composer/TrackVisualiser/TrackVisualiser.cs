// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Json.Burst;
using Godot;
using XanaduProject.ECSComponents.Animation;

namespace XanaduProject.Composer.TrackVisualiser
{
    public abstract partial class TrackVisualiser : Panel
    {
        public required Entity Entity;

        protected bool Exited = true;
        protected int  Offset = 20;

        protected int SelectedIndex = -1;
        public override void _EnterTree()
        {
            base._EnterTree();
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(0, 50);

            MouseExited += () =>
            {
                Exited = true;
                QueueRedraw();
            };
        }

        public override void _GuiInput(InputEvent @event) => QueueRedraw();
    }
}
