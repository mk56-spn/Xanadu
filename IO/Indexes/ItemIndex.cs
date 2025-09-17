// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

// For Item and ItemSerializer

namespace XanaduProject.IO.Indexes
{
    public static class ItemIndex
    {
        public record ItemInfo(string Name, string Author, string Description, string FilePath);

        private static Dictionary<string, ItemInfo> items { get; } = new();

        static ItemIndex()
            => BuildIndex();

        public static void BuildIndex()
        {
            items.Clear();
            string? itemsDir = ProjectSettings.GlobalizePath(SerializationUtils.ITEMS_DIR);

            if (!Directory.Exists(itemsDir))
            {
                GD.PrintErr($"ItemIndex: No items directory found at '{itemsDir}'.");
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(itemsDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    Item? item = JsonSerializer.Deserialize<Item>(json);

                    if (item == null)
                    {
                        GD.PrintErr($"ItemIndex: Failed to deserialize item from '{filePath}'. Skipping.");
                        continue;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (item.Name != fileName)
                    {
                        GD.PrintErr($"ItemIndex: Item name '{item.Name}' in file '{filePath}' does not match filename '{fileName}'. Using filename as key.");
                        // Optionally, you could enforce matching names or handle this differently.
                        // For now, we'll use the filename as the key for consistency with file system.
                        item = item with { Name = fileName };
                    }

                    string relativePath = SerializationUtils.ITEMS_DIR.PathJoin(Path.GetFileName(filePath));
                    var itemInfo = new ItemInfo(item.Name, item.Author, item.Description, relativePath);
                    items.Add(item.Name, itemInfo);
                }
                catch (JsonException e)
                {
                    GD.PrintErr($"ItemIndex: Failed to parse JSON from '{filePath}': {e.Message}");
                }
                catch (IOException e)
                {
                    GD.PrintErr($"ItemIndex: Failed to read file '{filePath}': {e.Message}");
                }
                catch (Exception e)
                {
                    GD.PrintErr($"ItemIndex: An unexpected error occurred while processing '{filePath}': {e.Message}");
                }
            }
        }

        public static ItemInfo? GetItemInfo(string name)
        {
            items.TryGetValue(name, out var itemInfo);
            return itemInfo;
        }

        public static Item? GetItem(string name)
        {
            ItemInfo? itemInfo = GetItemInfo(name);
            if (itemInfo == null)
            {
                return null;
            }

            try
            {
                // Use ItemSerializer to deserialize the full item
                return ItemSerializer.DeserializeItemFromFile(itemInfo.Name);
            }
            catch (Exception e)
            {
                GD.PrintErr($"ItemIndex: Failed to load full item '{name}' from '{itemInfo.FilePath}': {e.Message}");
                return null;
            }
        }

        public static IReadOnlyDictionary<string, ItemInfo> GetAllItemInfos()
        {
            return items;
        }
    }
}
