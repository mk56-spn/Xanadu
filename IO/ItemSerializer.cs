// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;
using Friflo.Engine.ECS;
using System.Text.Json;
using System.IO;
using static XanaduProject.IO.SerializationUtils;
using MemoryPack;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.ItemEditor;


namespace XanaduProject.IO
{
    public static class ItemSerializer
    {
        public static void SerializeItem(Item item)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(item, options);

            DirAccess.MakeDirRecursiveAbsolute(ITEMS_DIR);
            string filePath = Path.Combine(ITEMS_DIR, $"{item.Name}.json");
            File.WriteAllText(ProjectSettings.GlobalizePath(filePath), json);
        }

        public static Item? DeserializeItem(string json)
        {
            var options = new JsonSerializerOptions();
            return JsonSerializer.Deserialize<Item>(json, options);
        }

        public static Item? DeserializeItemFromFile(string itemName)
        {
            string filePath = Path.Combine(ITEMS_DIR, $"{itemName}.json");
            string absolutePath = ProjectSettings.GlobalizePath(filePath);

            if (!File.Exists(absolutePath))
            {
                GD.PrintErr($"Error: Item file not found at {absolutePath}");
                return null;
            }

            string json = File.ReadAllText(absolutePath);
            return DeserializeItem(json);
        }

        public static Item CreateItemFromComponentLayerManager(IComponentLayerManager componentLayerManager, string author, string description, string name)
        {
            var serializedComponents = new List<SerializableComponent>();

            foreach (Entity entity in componentLayerManager.GetAllLayerEntities())
            {
                if (entity.TryGetComponent(out MeshComponent meshComponent))
                {
                    serializedComponents.Add(new Mesh
                    {
                        MeshComponent = meshComponent,
                        Color = meshComponent.Color
                    });
                }
                // In the future, we can check for other component types here.
            }

            return new Item
            {
                Components = serializedComponents,
                Author = author,
                Description = description,
                Name = name
            };
        }
    }

    [MemoryPackable]
    public partial record Item(string Name = "New Item", string Description = "")
    {
        public List<SerializableComponent> Components { get; set; } = [];
        public string Author { get; set; } = "Anonymous";
        public string Description { get; set; } = Description;
        public string Name { get; set; } = Name;
    }


    [MemoryPackable]
    [MemoryPackUnion(0, typeof(Mesh))]
    public abstract partial record SerializableComponent;

    [MemoryPackable]
    public partial record Mesh : SerializableComponent
    {
        public required MeshComponent MeshComponent { get; set; }
        public Color Color { get; set; }
    }
}
