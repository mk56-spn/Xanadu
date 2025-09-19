using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.IO; // Added for Item

namespace XanaduProject.Scenes.MeshEditor
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
