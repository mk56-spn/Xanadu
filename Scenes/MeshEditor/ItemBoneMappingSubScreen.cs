using System.Diagnostics;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class ItemBoneMappingSubScreen : SubScreen
    {
        public ItemBoneMappingSubScreen(EntityStore store)
        {
            Visible = true;

            var label = new Label();
            label.Text = "Item Bone Mapping Subscreen";
            AddChild(label);

            var v = DiProvider.Get<EntityStore>();

            string itemName = ItemIndex.GetAllItemInfos().First().Value.Name;
            var item = ItemIndex.GetItem(itemName);

            Debug.Assert(item != null, nameof(item) + " != null");
            var rids = ItemBuilder.BuildItem(item, store);

            StandardPoseSkin s =  SkinIndex.GetAllSkins().First().Value;


            v.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
                foreach (var entityLink in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    var v = s.Bones.Where(c => c.Name == entityLink.Target.GetComponent<NameEcs>().Name);

                    foreach (var VARIABLE in v)
                    {
                        foreach (var renderRid in ItemBuilder.BuildItem(VARIABLE, store))
                        {
                            rootEcs.Canvas.AddChild(renderRid);
                        }
                    }
                }
                rootEcs.Canvas.SetTransform(new Transform2D(0, new Vector2(200, 200)));
                rootEcs.Canvas.SetParent(GetCanvasItem()).SetZIndex(100);
            });
        }
    }
}
