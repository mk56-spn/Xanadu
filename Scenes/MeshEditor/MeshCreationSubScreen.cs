using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshCreationSubScreen : SubScreen
    {
        public MeshCreationSubScreen()
        {
            AddChild(new MeshEditor());
            Visible = true;
        }
    }
}
