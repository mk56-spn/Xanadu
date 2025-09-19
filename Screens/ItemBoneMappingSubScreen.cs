using System.Diagnostics;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO.Indexes;
using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public partial class ItemBoneMappingSubScreen : SubScreen
    {
        public ItemBoneMappingSubScreen(EntityStore store)
        {
            Visible = true;
            // Initialize UI elements for item selection and bone mapping
            // For now, just a placeholder
            var label = new Label();
            label.Text = "Item Bone Mapping Subscreen";
            AddChild(label);

            var v = DiProvider.Get<EntityStore>();



            string itemName = ItemIndex.GetAllItemInfos().First().Value.Name;
            var item = ItemIndex.GetItem(itemName);

            Debug.Assert(item != null, nameof(item) + " != null");
            var rids = ItemBuilder.BuildItem(item, store);

            v.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
                foreach (var r in rids)
                {
                    rootEcs.Canvas.AddChild(r);
                }
                rootEcs.Canvas.SetTransform(new Transform2D(0, new Vector2(200, 200)));
                rootEcs.Canvas.SetParent(GetCanvasItem()).SetZIndex(100);
            });

        }
    }
}
