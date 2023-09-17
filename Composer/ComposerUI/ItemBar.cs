// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

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

        private Node2D selected = new EnvironmentPolygon();
        /// <summary>
        /// The currently selected ItemType
        /// </summary>
        public Node2D Selected
        {
            get => selected;
            set => selected = value;
        }

        public ItemBar ()
        {
            foreach (var itemType in composerItems())
                AddChild(new ItemButton(itemType));
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (!Input.IsKeyPressed(Key.Shift)) return;

            GetViewport().SetInputAsHandled();

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            addStageItem();
        }

        private void addStageItem()
        {
            Node2D newItem = (Node2D)selected.Duplicate();
            newItem.GlobalPosition = GetViewport().GetCamera2D().Offset + GetGlobalMousePosition();
            stage.AddChild(newItem);

            logItem(newItem);
        }

        #region Logging

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

        #endregion

        /// <summary>
        /// A list of all types that are to be put in the item bar.
        /// </summary>
        /// <returns></returns>
        private static Node2D[] composerItems() => new Node2D[]
        {
            new EnvironmentPolygon(),
            new ThreatPolygon(),
            ResourceLoader.Load<PackedScene>("res://Composer/Notes/HitNote.tscn").Instantiate<HitNote>(),
            new NoteLink()
        };
    }
}

