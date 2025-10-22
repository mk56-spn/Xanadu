using Friflo.Engine.ECS.Systems;
using XanaduProject.IO;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public partial class ItemCreationScreen : StoreScreen
    {
        public ItemCreationScreen(Item? item = null)
        {
            AddChild(new ItemEditor(item));
            Visible = true;
        }

        protected override void PostBaseSystems(SystemRoot root)
        {
        }
    }
}
