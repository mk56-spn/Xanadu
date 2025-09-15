using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Singletons;
using XanaduProject.Buttons;

namespace XanaduProject.Singleton
{
    public partial class LoggerOverlay : VBoxContainer
    {
        private readonly RichTextLabel logLabel;
        private HBoxContainer categoryContainer;

        public readonly Dictionary<LogCategory, Color> CategoryColors = new();
        public readonly Dictionary<LogCategory, Queue<string>> LogMessages = new();

        private readonly Dictionary<LogCategory, bool> categoryVisibility = new();
        private bool isLogDirty;

        public LoggerOverlay()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            foreach (LogCategory category in Enum.GetValues<LogCategory>())
            {
                LogMessages.Add(category, new Queue<string>());
                CategoryColors.Add(category, new Color(0.3f,0.3f,0.8f));
                categoryVisibility.Add(category, true);
            }

            logLabel = new RichTextLabel
            {
                Name = "LogLabel",
                CustomMinimumSize = new Vector2(0, 400),
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                ScrollFollowing = true,
                BbcodeEnabled = true,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetBottom = -30,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(logLabel);

            categoryContainer = new HBoxContainer
            {
                Name = "CategoryContainer",
                AnchorTop = 1,
                AnchorBottom = 1,
                AnchorRight = 1,
                OffsetTop = -30
            };
            AddChild(categoryContainer);

            setupCategoryButtons();
            isLogDirty = true;

            logLabel.AddThemeFontOverride("normal_font",FontSource.GOTHIC);
        }

        public override void _Ready()
        {
            base._Ready();
            SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            if (isLogDirty)
            {
                updateLog();
                isLogDirty = false;
            }
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);
            if (@event is InputEventKey { Keycode: Key.F12, Pressed: true })
            {
                Visible = !Visible;
            }
        }

        private void setupCategoryButtons()
        {
            foreach (var category in Enum.GetValues<LogCategory>())
            {
                var button = new Button { Text = category.ToString() };
                button.Pressed += () => OnCategoryButtonPressed(category);
                categoryContainer.AddChild(button);
            }
        }

        private void updateLog()
        {
            logLabel.Clear();
            foreach (var (category, messages) in LogMessages)
            {
                if (!categoryVisibility[category]) continue;

                var color = CategoryColors[category];
                foreach (string message in messages)
                {
                    logLabel.PushColor(color);
                    logLabel.AppendText($"{category.ToString().ToUpper()} {message}\n");
                    logLabel.Pop();
                }
            }
        }

        public void SetCategoryVisible(LogCategory category, bool visible)
        {
            categoryVisibility[category] = visible;
            isLogDirty = true;
        }

        private void OnCategoryButtonPressed(LogCategory category)
        {
            categoryVisibility[category] = !categoryVisibility[category];
            isLogDirty = true;
        }

        public void SetLogDirty()
        {
            isLogDirty = true;
        }
    }
}
