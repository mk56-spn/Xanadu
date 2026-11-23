using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Projects backing text-field values into drawable ECS text.</summary>
public sealed class TextFieldSystem : QuerySystem<TextField, Text, FillColor>
{
    private static readonly Color FocusColor = new("#172554");
    private static readonly Color NormalColor = Palette.SurfaceInset;

    protected override void OnUpdate()
    {
        Query.EachEntity(new SyncFields(Query.Store, CommandBuffer));
    }

    private readonly record struct SyncFields(EntityStore Store, CommandBuffer Buffer) : IEachEntity<TextField, Text, FillColor>
    {
        public void Execute(ref TextField field, ref Text text, ref FillColor fill, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Color target = entity.Tags.Has<TextFieldFocused>()
                ? FocusColor
                : field.Fill ?? NormalColor;
            if (fill.Value != target)
            {
                fill.Value = target;
                Buffer.AddTag<Dirty>(id);
            }

            if (text.Value == field.Value)
                return;

            text.Value = field.Value;
            Buffer.AddTag<Dirty>(id);
        }
    }
}
