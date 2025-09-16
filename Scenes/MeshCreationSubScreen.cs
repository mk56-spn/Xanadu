using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Scenes
{
    public partial class MeshCreationSubScreen : SubScreen
    {
        public MeshCreationSubScreen()
        {
            AddChild(new MeshEditor.MeshEditor());
        }
    }
}
