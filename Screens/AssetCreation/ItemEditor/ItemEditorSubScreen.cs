using System;
using XanaduProject.IO;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public partial class ItemEditorSubScreen : SubScreen
    {
        private readonly ItemEditor itemEditor;

        public event Action<Item>? ItemSaved;

        public ItemEditorSubScreen(Item item)
        {

            // Make the subscreen visible and fill the entire parent.
            Visible = true;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            itemEditor = new ItemEditor(item);
            AddChild(itemEditor);

            itemEditor.ItemSaved += OnItemSaved;
        }

        private void OnItemSaved(Item savedItem)
        {
            ItemSaved?.Invoke(savedItem);
            // After saving, we can close this subscreen.
            QueueFree();
        }
    }
}
