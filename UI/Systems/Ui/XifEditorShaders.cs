using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// The XIF editor's shader controls: the tabbed SHADER picker and the + LAYER list,
/// each pick applied to the open item through its host.
/// </summary>
public static class XifEditorShaders
{
    /// <summary>
    /// A pick in the + LAYER list (<see cref="UiDropdowns.OnPicked"/> on
    /// <see cref="XifEditorParts.LayerAdd"/>): stacks that shader over the open item.
    /// </summary>
    public static void AddLayer(Entity panel, int pick)
    {
        if (panel.IsNull || !panel.HasComponent<XifEditor>())
            return;

        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        Xif? item = editor.Open ? editor.Host.Item : null;
        IReadOnlyList<XifShaderChoice> choices = editor.Host.Layers;
        if (item is null || pick < 0 || pick >= choices.Count)
            return;
        string path = choices[pick].Shader.Path;
        string chosen = choices[pick].Label;
        bool added = false;
        editor.Host.Edit(() => added = XifEditing.AddLayer(item, path));
        editor = ref panel.GetComponent<XifEditor>();
        editor.Status = added ? $"LAYER {chosen} ADDED" : $"{chosen} IS ALREADY IN THE STACK";
        Entity add = editor.Parts.LayerAdd;
        if (!add.TryGetComponent(out Text shown) || shown.Value != XifEditorPanel.AddLayerText)
            UiText.Set(add, XifEditorPanel.AddLayerText);
    }

    /// <summary>The shader picker's pick: draws the item with that choice.</summary>
    public static void PickShader(Entity panel, int tab, int entry)
    {
        if (panel.IsNull || !panel.HasComponent<XifEditor>())
            return;

        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        Xif? item = editor.Open ? editor.Host.Item : null;
        int index = XifShaderTabs.IndexOf(editor.Host.Shaders, tab, entry);
        if (item is null || index < 0)
            return;
        XifShaderChoice choice = editor.Host.Shaders[index];
        editor.Host.Edit(() => XifEditing.SetShader(item, choice.Shader));
        editor = ref panel.GetComponent<XifEditor>();
        editor.Status = $"SHADER {choice.Label}";
    }

    /// <summary>What a layer's row calls it: the host's label for it, else its file's name.</summary>
    internal static string LayerLabel(IXifEditorHost host, string path)
    {
        foreach (XifShaderChoice choice in host.Layers)
        {
            if (choice.Shader.Path == path)
                return choice.Label;
        }
        return System.IO.Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
    }

    private static int ShaderIndex(IReadOnlyList<XifShaderChoice> choices, XifShaderRef shader)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].Shader.Same(shader))
                return i;
        }
        return -1;
    }

    /// <summary>Shows the item's shader on the picker; one the host doesn't offer reads as not allowed.</summary>
    internal static void Show(in XifEditor editor, Xif item)
    {
        XifShaderRef current = item.ShaderRef;
        int index = ShaderIndex(editor.Host.Shaders, current);
        (int tab, int entry) = XifShaderTabs.PlaceOf(editor.Host.Shaders, index);
        UiTabbedPickers.Show(editor.Parts.Shader, tab, entry);
        if (index < 0 && !current.IsNone)
            UiText.Set(UiTabbedPickers.ButtonOf(editor.Parts.Shader), "NOT ALLOWED HERE ▼");
    }
}
