// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using static XanaduProject.ECSComponents.Presets.PrefabEntity;
using static XanaduProject.ECSComponents.RectEcs;

namespace XanaduProject.Composer
{
    internal partial class ButtonContainer : VBoxContainer
    {
        public ComposerRenderMaster Composer = null!;

        private Vector2 mouse => Composer.GetLocalMousePosition();
        public override void _Ready()
        {
            Composer.Action = c =>
                Block(c, position(PRESETS[0]), PRESETS[0]);

            createButton("Hurt ⚔️", c => Hurt(c, position(PRESETS[0]), Composer.GetWorld2D()));
            createButton("Note \u266b", c => createNote(c, LARGE));
            createButton("Arrow note \u266b", c => createNoteDirectional(c, LARGE));
            createButton("Polygon", c => Polygon(c, position(new Vector2(32, 32)), PolygonEcs.DEFAULT_POINTS));

            foreach (var extent in PRESETS)
                createButton (extent.X + " " + extent.Y + "\u25a0", c => Block(c, position(extent), extent));

            return;

            Vector2 position(Vector2 size)
            {
                return (Composer.GetGlobalMousePosition() + size / 2).Snapped(size) - size / 2;
            }
        }

        private void createButton(string name, Action<Entity> action)
        {
            var button = new ItemButton
            {
                Text = name,
                CustomMinimumSize = new Vector2(0,50)
            };
            button.Pressed += () => Composer.Action = action;
            AddChild(button);
        }

        private void createNote(Entity c, Vector2 size)
        {
            Note(c, (mouse + size / 2).Snapped(size) - size / 2,
                (float)Mathf.Snapped(Composer.TrackHandler.TrackPosition, 60 / 200f), Composer.GetWorld2D());
        }

        private void createNoteDirectional(Entity c, Vector2 size)
        {
            DirectionalNote(c, (mouse + size / 2).Snapped(size) - size / 2,
                (float)Mathf.Snapped(Composer.TrackHandler.TrackPosition, 60 / 200f), Composer.GetWorld2D());
        }


        private void createRect(Entity c, Vector2 size)
        {
            Rect(c, (mouse + size / 2).Snapped(size) - size / 2, size);
        }

        private partial class ItemButton : Button
        {
            public ItemButton()
            {
                Modulate = Colors.White.Darkened(0.4f);
                MouseExited += () =>
                    CreateTween().TweenProperty(this, "modulate", Colors.White.Darkened(0.4f), 0.3f);
                MouseEntered += () => CreateTween().TweenProperty(this, "modulate", Colors.White, 0.3f);
            }
        }
    }
}
