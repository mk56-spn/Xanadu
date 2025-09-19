// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;
using XanaduProject.Scenes.MeshEditor;
using Friflo.Engine.ECS;
using System.Text.Json;
using System.IO;
using static XanaduProject.IO.SerializationUtils;
using System.Text.Json.Serialization;

namespace XanaduProject.IO
{
    public static class ItemSerializer
    {
        public static void SerializeItem(Item item)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            // The converter is now applied via attribute on SerializableMeshLayer.Color
            // options.Converters.Add(new ColorConverter());
            string json = JsonSerializer.Serialize(item, options);

            // Ensure the directory exists
            DirAccess.MakeDirRecursiveAbsolute(ITEMS_DIR);
            string filePath = Path.Combine(ITEMS_DIR, $"{item.Name}.json");
            File.WriteAllText(ProjectSettings.GlobalizePath(filePath), json);
        }

        public static Item? DeserializeItem(string json)
        {
            var options = new JsonSerializerOptions();
            // The converter is now applied via attribute on SerializableMeshLayer.Color
            // options.Converters.Add(new ColorConverter());
            return JsonSerializer.Deserialize<Item>(json, options);
        }

        public static Item DeserializeItemFromFile(string itemName)
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

        public static Item CreateItemFromMeshLayerManager(IMeshLayerManager meshLayerManager, string author, string description, string name)
        {
            List<SerializableMeshLayer> serializedLayers = new List<SerializableMeshLayer>();

            foreach (Entity entity in meshLayerManager.GetAllMeshEntities())
            {
                if (entity.TryGetComponent(out MeshComponent meshComponent))
                {
                    serializedLayers.Add(new SerializableMeshLayer
                    {
                        MeshComponent = meshComponent,
                        Color = meshComponent.Color
                    });
                }
            }

            return new Item
            {
                MeshLayers = serializedLayers,
                Author = author,
                Description = description,
                Name = name
            };
        }
    }

    public record Item()
    {
        public List<SerializableMeshLayer> MeshLayers { get; set; } = new();
        public string Author { get; set; } = "Anonymous";
        public string Description { get; set; }
        public string Name { get; set; }
    }

    public record SerializableMeshLayer
    {
        public required MeshComponent MeshComponent { get; set; }
        [JsonConverter(typeof(ColorConverter))] // Apply the custom converter directly to the property
        public Color Color { get; set; }
    }
}
