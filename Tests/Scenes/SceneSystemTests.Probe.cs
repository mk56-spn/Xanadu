using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Layout;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.Scenes;

public sealed partial class SceneSystemTests
{
    [Test]
    public void ProbeTrack()
    {
        AddLevels(20);
        _root.Add(new ContainerLayoutSystem());
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        Frame();
        foreach (Vector2 p in new[]
                 {
                     new Vector2(20, 540), new Vector2(40, 400), new Vector2(40, 540), new Vector2(60, 540),
                     new Vector2(40, 700)
                 })
        {
            TestContext.Out.WriteLine($"-- {p}");
            _store.Query<Rectangle, Drawable, Position>().AllTags(Tags.Get<TakesInput>()).ForEachEntity((
                ref Rectangle r, ref Drawable d, ref Position pos, Entity e) =>
            {
                Vector2 w = UiTransform.WorldPosition(e, pos.Value);
                if (new Rect2(w + r.Bounds.Position, r.Bounds.Size).HasPoint(p))
                    TestContext.Out.WriteLine(
                        $"{e.Id} z={d.ZIndex} rect={new Rect2(w + r.Bounds.Position, r.Bounds.Size)} disabled={e.Tags.Has<UiDisabled>()} {string.Join(",", e.Components)}");
            });
        }
    }
}
