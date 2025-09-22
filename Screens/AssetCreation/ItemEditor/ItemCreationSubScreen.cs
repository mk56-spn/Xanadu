using XanaduProject.IO;
using XanaduProject.Screens.ScreenStructure;

// Added for Item

namespace XanaduProject.Scenes.ItemEditor
{
    public partial class ItemCreationSubScreen : SubScreen
    {
        public ItemCreationSubScreen(Item? item = null)
        {
            AddChild(new ItemEditor(item));
            Visible = true;
        }
    }
}
