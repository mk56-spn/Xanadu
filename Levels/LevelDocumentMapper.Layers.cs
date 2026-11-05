using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Layering;

namespace Xanadu.Levels;

/// <summary>The layer table between the document and <see cref="LevelLayers"/> on the level's entity.</summary>
public static partial class LevelDocumentMapper
{
    /// <summary>Every layer with settings, back to front.</summary>
    private static List<LayerDocument> CaptureLayers(Entity editor)
    {
        var result = new List<LayerDocument>();
        LevelLayers table = LevelLayers.Of(editor);
        foreach ((int number, LevelLayer layer) in table.Layers.OrderBy(entry => entry.Key))
        {
            if (!layer.HasSettings)
                continue;

            result.Add(new LayerDocument
            {
                Number = number,
                Name = layer.Name,
                Pass = layer.Pass,
                PassShader = layer.PassShader,
                PassKeys = layer.PassKeys.Select(key => new ShaderKeyDocument
                {
                    Param = key.Param,
                    Time = key.Time,
                    Value = Components(key.Value, key.Size),
                    Ease = key.Ease,
                }).ToList(),
            });
        }

        return result;
    }

    /// <summary>
    /// Puts the document's layer table on the level's entity. Entries for layer
    /// 0 or out of range are dropped, and a later entry for the same number
    /// replaces an earlier one.
    /// </summary>
    private static void ApplyLayers(Entity editor, List<LayerDocument>? documents)
    {
        LevelLayers table = LevelLayers.Create();
        foreach (LayerDocument document in documents ?? new List<LayerDocument>())
        {
            if (document.Number == 0 || DrawOrder.ClampLayer(document.Number) != document.Number)
                continue;

            var layer = new LevelLayer
            {
                Name = document.Name ?? "",
                Pass = document.Pass,
                PassShader = document.PassShader,
            };
            foreach (ShaderKeyDocument key in document.PassKeys ?? new List<ShaderKeyDocument>())
            {
                float[] value = key.Value ?? [];
                int size = Math.Min(value.Length, ShaderKey.MaxSize);
                layer.PassKeys.Add(new ShaderKey(key.Param ?? "", key.Time, Vector(value, size), size, key.Ease));
            }

            if (layer.HasSettings)
                table.Layers[document.Number] = layer;
        }

        // A level with no layer settings needs no component, so applying one
        // inside an editor command makes no structural change.
        if (editor.HasComponent<LevelLayers>())
            editor.GetComponent<LevelLayers>() = table;
        else if (table.Layers.Count > 0)
            editor.AddComponent(table);
    }

    private static Vector4 Vector(float[] value, int size) => new(
        size > 0 ? value[0] : 0,
        size > 1 ? value[1] : 0,
        size > 2 ? value[2] : 0,
        size > 3 ? value[3] : 0);

    private static float[] Components(Vector4 value, int size)
    {
        float[] all = [value.X, value.Y, value.Z, value.W];
        return all[..Math.Clamp(size, 0, ShaderKey.MaxSize)];
    }
}
