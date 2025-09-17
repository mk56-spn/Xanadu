using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.IO; // Added for Item

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshCreationSubScreen : SubScreen
    {
        public MeshCreationSubScreen(Item? item = null)
        {
            AddChild(new MeshEditor(item));
            Visible = true;
        }
    }
}
