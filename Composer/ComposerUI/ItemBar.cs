// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Composer.Notes;
using XanaduProject.Screens;

namespace XanaduProject.Composer.ComposerUI
{
    /// <summary>
    /// An attachment for the composer that handles placing objects in the <see cref="Stage"/>
    /// </summary>
    [SuperNode(typeof(Dependent))]
    public partial class ItemBar : VBoxContainer
    {
        public override partial void _Notification(int what);

        [Export]
        private VBoxContainer logContainer = null!;

        [Dependency]
        private Stage stage => DependOn<Stage>();
        [Dependency]
        private Camera2D camera => DependOn<Camera2D>();

        /// <summary>
        /// The currently selected ItemType
        /// </summary>
        public ItemType Selected { get; set; }

        public ItemBar ()
        {
            foreach (var itemType in Enum.GetValues(typeof(ItemType)).OfType<ItemType>())
                AddChild(new ItemButton(itemType));
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || !Input.IsKeyPressed(Key.Shift)) return;
            addStageItem();
        }

        private void addStageItem()
        {
            Node2D newItem = (Node2D)selected.Duplicate();
            newItem.GlobalPosition = GetViewport().GetCamera2D().Offset + GetGlobalMousePosition();
            stage.AddChild(newItem);

            logItem(newItem);
        }

        private static Node2D getItemInstance(ItemType type) =>
            type switch
            {
                // Im gonna regret this at some point...
                ItemType.NoteLink => new NoteLink(),
                ItemType.HitNote => ResourceLoader.Load<PackedScene>("res://Composer/Notes/HitNote.tscn")
                    .Instantiate<HitNote>(),
                ItemType.EnvironmentPolygon => new EnvironmentPolygon(),
                ItemType.ThreatPolygon => new ThreatPolygon(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

        private void logItem(Node2D newItem)
        {
            Label label = new Label();
            label.TreeEntered += () =>
            {
                CreateTween().TweenProperty(label, "modulate", label.Modulate with { A = 0 }, 1.5);
                GetTree().CreateTimer(1.5).Timeout += () =>
                    label.QueueFree();
            };

            label.Text = $"{newItem.GetType().Name} added.";

            logContainer.AddChild(label);
            logContainer.MoveChild(label, 0);
        }
    }

    // An enum used to fetch the corresponding class for instantiation.
    public enum ItemType
    {
        NoteLink,
        HitNote,
        EnvironmentPolygon,
        ThreatPolygon
    }
}

