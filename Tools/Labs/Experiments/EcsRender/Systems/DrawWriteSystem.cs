using System;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>
/// The instances into the upload buffer in draw order, and the draws they
/// make: one, unless glyphs from different atlas pages follow each other
/// (shapes need no texture, so they never end a draw).
/// </summary>
internal sealed class DrawWriteSystem(DrawList list, InstancePass pass) : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        if (list.Count == 0)
            return;

        Span<DrawInstance> mapped = pass.Map(list.Count);
        uint start = 0;
        short bound = DrawList.NoPage;
        for (int i = 0; i < list.Count; i++)
        {
            int index = (int)(uint)list.Keys[i];
            short page = list.Pages[index];
            if (page != DrawList.NoPage && bound != DrawList.NoPage && page != bound)
            {
                list.Draws.Add((start, (uint)i - start, bound));
                start = (uint)i;
            }

            if (page != DrawList.NoPage)
                bound = page;
            mapped[i] = list.Instances[index];
        }

        list.Draws.Add((start, (uint)list.Count - start, bound));
        pass.Unmap();
    }
}
