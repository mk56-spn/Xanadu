using System.Collections.Generic;
using Godot;

namespace XanaduProject.Tests;

[GlobalClass]
public partial class InputTest : CenterContainer
{
    public InputTest()
    {
        var container = new HBoxContainer();
        AddChild(container);

        foreach (var child in _addKeys)
            container.AddChild(child);
    }

    private readonly List<InputRect> _addKeys = new()
    {
        new InputRect
        {
            Position = new Vector2(400,400),
            Key = "main",
            ColourOn = Colors.Yellow
        },
        new InputRect
        {
            Position = new Vector2(400,400),
            Key = "R1",
            ColourOn = Colors.Green
        },
        new InputRect
        {
            Position = new Vector2(400,400),
            Key = "R2",
            ColourOn = Colors.Orange
        }
    };

    private ColorRect InputBox() =>
        new() { Size = new Vector2(100, 100) };

    private partial class InputRect : ColorRect
    {
        public Color ColourOn { get; set; }
        public string Key { get; set; }

        public InputRect() =>
            CustomMinimumSize = new Vector2(100, 100);

        public override void _Ready()
        {
            base._Ready();

            AddChild(new Label
            {
                Text = Key,
                LayoutMode = 1, 
                AnchorsPreset = 8,
            });
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            Color = Input.IsActionPressed(Key) ? ColourOn : Colors.Black;
        }
    }
}