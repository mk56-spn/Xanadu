using Friflo.Engine.ECS;
using Godot;
using Position = Xanadu.Ecs.Position;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Timeline;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Groups in the editor: the sidebar grid opens a group's overlay, selected
/// items join it, effects are added and retimed on the snapping timeline, the
/// overlay closes again, and the main view shows the group live at the playhead.
/// </summary>
public sealed class EditorGroupsSceneTest : SceneTest
{
    private Entity _note;
    private Entity _start;
    private Entity _block;
    private Vector2 _offsetBefore;

    public override string Name => "Editor groups";
    public override string Slug => "editor-groups";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the groups grid",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorGroupCell>().Count == GroupLimits.Count &&
                  VisualAssert.HasText(Context.Entities, UiCollapsibleControl.Header("GROUPS", expanded: true)));
        AddEditorSong();
        AddAssert("the overlay starts closed", () => State().Open == -1);

        AddStep("place and select a note and a start point", () =>
        {
            _note = EditorItems.Create(Context.Entities, new Note(2_000, new Vector2(400, 400)));
            _start = EditorItems.Create(Context.Entities, new StartPoint(
                "Intro",
                2_000,
                new Vector2(300, 300),
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1)));
            Editor().GetComponent<NoteSelection>().SetMany(new[] { _note, _start });
        });

        AddStep("click group 5 in the grid", () => Click(Cell(4)));
        AddWaitStep("hold group 5", 2);
        AddStep("release group 5", () => Context.Pointer.Release());
        AddUntilStep("the grid put the selection in group 5", () =>
            _note.TryGetComponent(out ItemGroups g) && g.Has(4) &&
            _start.TryGetComponent(out ItemGroups s) && s.Has(4));
        AddAssert("the grid opened nothing", () => State().Open == -1);
        AddStep("take the note's shader from group 5", () => ItemShaderSource.Put(_note, 4));
        AddStep("click group 5 again", () => Click(Cell(4)));
        AddWaitStep("hold group 5 again", 2);
        AddStep("release group 5 again", () => Context.Pointer.Release());
        AddUntilStep("the grid took the selection back out", () =>
            !(_note.TryGetComponent(out ItemGroups g) && g.Has(4)) &&
            !(_start.TryGetComponent(out ItemGroups s) && s.Has(4)));
        AddAssert("leaving the group put the note's shader back to its own", () =>
            ItemShaderSource.Of(_note) == ItemShaderSource.Own);

        for (int i = 0; i < 2; i++)
        {
            AddStep($"step the bar up {i + 1}", () => Click(Bar().Next));
            AddWaitStep($"hold + {i + 1}", 2);
            AddStep($"release + {i + 1}", () => Context.Pointer.Release());
        }

        AddUntilStep("the bar points at group 3", () => State().BarGroup == 2);
        AddStep("click EDIT", () => Click(Bar().Edit));
        AddWaitStep("hold EDIT", 2);
        AddStep("release EDIT", () => Context.Pointer.Release());
        AddUntilStep("the overlay opens on group 3", () => State().Open == 2 && Visible(EditorGroupPart.Dialog));

        AddStep("click ADD SELECTED", () => Click(Piece(EditorGroupPart.AddSelected)));
        AddWaitStep("hold ADD SELECTED", 2);
        AddStep("release ADD SELECTED", () => Context.Pointer.Release());
        AddUntilStep("the note joined the group", () => _note.TryGetComponent(out ItemGroups g) && g.Has(2));
        AddUntilStep("the start point joined the group", () => _start.TryGetComponent(out ItemGroups g) && g.Has(2));

        AddStep("click + MOVE", () => Click(Piece(EditorGroupPart.AddMove)));
        AddWaitStep("hold + MOVE", 2);
        AddStep("release + MOVE", () => Context.Pointer.Release());
        AddUntilStep("a move was added", () => Group().Effects.Count == 1);

        AddStep("click + COLOR", () => Click(Piece(EditorGroupPart.AddColor)));
        AddWaitStep("hold + COLOR", 2);
        AddStep("release + COLOR", () => Context.Pointer.Release());
        AddUntilStep("a colour was added", () => Group().Effects.Count == 2);
        AddUntilStep("the rows are shown", () => Visible(EditorGroupPart.RowKind, 0) && Visible(EditorGroupPart.RowKind, 1));

        AddStep("click the move's EASE", () => Click(Piece(EditorGroupPart.RowEase, 0)));
        AddWaitStep("hold EASE", 2);
        AddStep("release EASE", () => Context.Pointer.Release());
        AddUntilStep("the ease picker opens for the move", () =>
            State().EaseFor == EditorEaseTarget.Effect && EasePickerPanelEntity().Enabled);
        AddStep("pick QUAD IN", () => Click(EaseCell(EasePreset.EaseIn)));
        AddWaitStep("hold the curve", 2);
        AddStep("release the curve", () => Context.Pointer.Release());
        AddUntilStep("the move eases in", () => Group().Effects[0].Ease == EasePreset.EaseIn);
        AddAssert("the colour keeps linear easing", () => Group().Effects[1].Ease == EasePreset.Linear);
        AddStep("close the picker with its EASE button", () => Click(Piece(EditorGroupPart.RowEase, 0)));
        AddWaitStep("hold EASE", 2);
        AddStep("release EASE", () => Context.Pointer.Release());
        AddUntilStep("the picker is closed", () => State().EaseFor == EditorEaseTarget.None && !EasePickerPanelEntity().Enabled);

        AddStep("select the colour row", () => Context.Pointer.Click(Bar(1)));
        AddWaitStep("hold the colour bar", 2);
        AddStep("release the colour bar", () => Context.Pointer.Release());
        AddUntilStep("the colour is selected", () => State().SelectedEffect == 1);

        AddStep("open the colour picker", () => Click(Piece(EditorGroupPart.SelectionPick)));
        AddWaitStep("hold PICK", 2);
        AddStep("release PICK", () => Context.Pointer.Release());
        AddUntilStep("the palette opens", () => State().PaletteOpen && !UiLayout.IsHidden(Piece(EditorGroupPart.PalettePanel)));

        AddStep("pick a yellow swatch", () => Click(Piece(EditorGroupPart.PaletteSwatch, 4)));
        AddWaitStep("hold the swatch", 2);
        AddStep("release the swatch", () => Context.Pointer.Release());
        AddUntilStep("the colour changes", () => Group().Effects[1].Tint.G > 0.7f);

        AddStep("close the palette", () => Click(Piece(EditorGroupPart.PaletteClose)));
        AddWaitStep("hold palette close", 2);
        AddStep("release palette close", () => Context.Pointer.Release());
        AddUntilStep("the palette closes", () => !State().PaletteOpen);

        AddStep("select the move row", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the move bar", 2);
        AddStep("release the move bar", () => Context.Pointer.Release());
        AddUntilStep("the move is selected", () => State().SelectedEffect == 0);

        AddStep("nudge the move later", () => Click(Piece(EditorGroupPart.SelectionNudgeRight)));
        AddWaitStep("hold nudge right", 2);
        AddStep("release nudge right", () => Context.Pointer.Release());
        AddUntilStep("the move starts later", () => Group().Effects[0].Start > 0f);

        AddStep("nudge the move back", () => Click(Piece(EditorGroupPart.SelectionNudgeLeft)));
        AddWaitStep("hold nudge left", 2);
        AddStep("release nudge left", () => Context.Pointer.Release());
        AddUntilStep("the move is back at zero", () => Group().Effects[0].Start <= 0.001f);

        AddStep("mute the move", () => Click(Piece(EditorGroupPart.SelectionMute)));
        AddWaitStep("hold MUTE", 2);
        AddStep("release MUTE", () => Context.Pointer.Release());
        AddUntilStep("the move is muted", () => Group().Effects[0].Muted);

        AddStep("unmute the move", () => Click(Piece(EditorGroupPart.SelectionMute)));
        AddWaitStep("hold UNMUTE", 2);
        AddStep("release UNMUTE", () => Context.Pointer.Release());
        AddUntilStep("the move is unmuted", () => !Group().Effects[0].Muted);

        AddStep("duplicate the move", () => Click(Piece(EditorGroupPart.SelectionDuplicate)));
        AddWaitStep("hold DUP", 2);
        AddStep("release DUP", () => Context.Pointer.Release());
        AddUntilStep("the move is duplicated", () => Group().Effects.Count == 3 && State().SelectedEffect == 1);

        AddStep("delete the duplicate", () => Click(Piece(EditorGroupPart.RowDelete, 1)));
        AddWaitStep("hold duplicate delete", 2);
        AddStep("release duplicate delete", () => Context.Pointer.Release());
        AddUntilStep("the duplicate is deleted", () => Group().Effects.Count == 2);

        AddStep("select the move row again", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the move bar again", 2);
        AddStep("release the move bar again", () => Context.Pointer.Release());
        AddUntilStep("the move is selected again", () => State().SelectedEffect == 0);

        AddStep("remember the move offset", () => _offsetBefore = Group().Effects[0].Offset);
        AddStep("open the target on the canvas", () => Click(Piece(EditorGroupPart.SelectionTarget)));
        AddWaitStep("hold TARGET", 2);
        AddStep("release TARGET", () => Context.Pointer.Release());
        AddUntilStep("target editing starts", () => State().TargetEdit && State().Open == -1);

        AddStep("press the target handle", () => Context.Pointer.Click(TargetHandle()));
        AddWaitStep("hold the target", 2);
        AddStep("drag the target", () => Context.Pointer.Position += new Vector2(40, -30));
        AddWaitStep("drag the target", 3);
        AddStep("release the target", () => Context.Pointer.Release());
        AddUntilStep("the move offset changes", () => Group().Effects[0].Offset != _offsetBefore);

        AddStep("reopen group 3", () => Click(Bar().Edit));
        AddWaitStep("hold group 3 again", 2);
        AddStep("release group 3 again", () => Context.Pointer.Release());
        AddUntilStep("the overlay reopens", () => State().Open == 2 && !State().TargetEdit);

        AddStep("solo the group", () => Click(Piece(EditorGroupPart.SoloToggle)));
        AddWaitStep("hold SOLO", 2);
        AddStep("release SOLO", () => Context.Pointer.Release());
        AddUntilStep("the group is soloed", () => State().SoloGroup == 2);

        AddStep("unsolo the group", () => Click(Piece(EditorGroupPart.SoloToggle)));
        AddWaitStep("hold SOLO again", 2);
        AddStep("release SOLO again", () => Context.Pointer.Release());
        AddUntilStep("the solo is off", () => State().SoloGroup == -1);

        AddStep("play the live preview", () => Click(Piece(EditorGroupPart.PlayToggle)));
        AddWaitStep("hold PLAY", 2);
        AddStep("release PLAY", () => Context.Pointer.Release());
        AddUntilStep("the preview plays", () => Editor().GetComponent<Playhead>().Playing);

        AddStep("pause the live preview", () => Click(Piece(EditorGroupPart.PlayToggle)));
        AddWaitStep("hold PAUSE", 2);
        AddStep("release PAUSE", () => Context.Pointer.Release());
        AddUntilStep("the preview pauses", () => !Editor().GetComponent<Playhead>().Playing);

        AddStep("scrub the group timeline to 2 s", () => Context.Pointer.Click(RulerPoint(2f)));
        AddWaitStep("hold the ruler", 2);
        AddStep("release the ruler", () => Context.Pointer.Release());
        AddUntilStep("the overlay playhead moves", () =>
            System.Math.Abs(Editor().GetComponent<Playhead>().TimeMs - 2_000) < 30);
        AddAssert("the start point is tinted at two seconds", () =>
        {
            LevelGroups groups = Editor().GetComponent<LevelGroups>();
            return _start.TryGetComponent(out ItemGroups member) &&
                   GroupModulation.TryColor(
                       groups.Groups, member.Mask, 2f, out Color mix, out _, out _) &&
                   mix.G > 0.7f;
        });

        AddStep("press the move's bar", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the bar", 2);
        AddStep("drag the bar right", () => Context.Pointer.Position += new Vector2(160, 0));
        AddWaitStep("drag", 3);
        AddStep("release the bar", () => Context.Pointer.Release());
        AddAssert("the bar moved later and kept its length", () =>
            Group().Effects[0].Start > 0.4f &&
            System.Math.Abs((Group().Effects[0].End - Group().Effects[0].Start) - 0.5f) < 0.01f);
        AddAssert("dragging kept the easing", () => Group().Effects[0].Ease == EasePreset.EaseIn);

        AddStep("click DONE", () => Click(Piece(EditorGroupPart.Close)));
        AddWaitStep("hold DONE", 2);
        AddStep("release DONE", () => Context.Pointer.Release());
        AddUntilStep("the overlay closes", () => State().Open == -1 && !Visible(EditorGroupPart.Dialog));

        AddStep("put a block in the group", () =>
        {
            _block = EditorItems.Create(Context.Entities, new Block(new Vector2(2300, 4400), new Vector2(240, 120)));
            _block.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        });
        AddStep("seek to five seconds", () => Seek(5_000));
        AddUntilStep("the note is shown moved by its group", () =>
            _note.TryGetComponent(out GroupOffset moved) &&
            moved.Offset.IsEqualApprox(Group().Effects[0].Offset) &&
            moved.Offset != Vector2.Zero);
        AddAssert("its placed position is untouched", () =>
            _note.GetComponent<Note>().Position == new Vector2(400, 400));
        AddUntilStep("the block is shown moved too", () => GroupOffset.Of(_block) == GroupOffset.Of(_note));
        AddStep("select only the block", () => Editor().GetComponent<NoteSelection>().SetMany(new[] { _block }));
        AddUntilStep("the start point is shown recoloured", () =>
            _start.TryGetComponent(out GroupColor tint) && tint.Coverage > 0.9f);
        AddWaitStep("look at the live editor", 5);

        AddStep("reopen group 3 over the live field", () => Click(Bar().Edit));
        AddWaitStep("hold group 3 once more", 2);
        AddStep("release group 3 once more", () => Context.Pointer.Release());
        AddUntilStep("the panel is docked at the top of the view", () =>
            State().Open == 2 &&
            Visible(EditorGroupPart.Dialog) &&
            Piece(EditorGroupPart.Dialog).GetComponent<Position>().Value.Y <
                Editor().GetComponent<PlayfieldView>().ScreenSize.Y / 3);
        AddWaitStep("look at the panel over the field", 2);

        AddStep("give the level a shader with an instance uniform", () =>
        {
            LevelShaders shaders = Editor().GetComponent<LevelShaders>();
            shaders.Slots[0] = new ShaderSlot
            {
                Name = "Track Test",
                Source = "[Instance] uniform float4 tint;\nfloat4 shade(ItemInput i) { return tint; }",
            };
        });
        AddStep("open the SHD list", () => Click(Piece(EditorGroupPart.ShaderSlot)));
        AddWaitStep("hold SHD", 2);
        AddStep("release SHD", () => Context.Pointer.Release());
        AddUntilStep("the list names the shaders", () =>
            Piece(EditorGroupPart.ShaderSlot).GetComponent<Dropdown>().Open &&
            VisualAssert.HasText(Context.Entities, "1  TRACK TEST"));
        AddWaitStep("look at the shader list", 3);
        AddStep("choose the test shader", () => ClickShaderOption(1));
        AddWaitStep("hold the option", 2);
        AddStep("release the option", () => Context.Pointer.Release());
        AddUntilStep("the group's track uses slot 1", () =>
            Group().Shader == 1 &&
            Piece(EditorGroupPart.ShaderSlot).GetComponent<Xanadu.Ui.Ecs.Ui.Text>().Value.StartsWith("SHD 1 TRACK"));
        AddStep("raise the tint at the playhead", () => Click(Piece(EditorGroupPart.ShaderUp)));
        AddWaitStep("hold the shader plus", 2);
        AddStep("release the shader plus", () => Context.Pointer.Release());
        AddUntilStep("the track has a tint key", () =>
            Group().ShaderKeys.Count == 1 &&
            Group().ShaderKeys[0].Param == "tint" &&
            Group().ShaderKeys[0].Size == 4 &&
            Group().ShaderKeys[0].Value.X > 0f);
        AddWaitStep("look at the shader track", 3);

        AddStep("focus the first effect's start", () => Piece(EditorGroupPart.RowStart, 0).AddTag<TextFieldFocused>());
        AddStep("type a start", () => Piece(EditorGroupPart.RowStart, 0).GetComponent<TextField>().Value = "1.5");
        AddStep("leave the start", () => Piece(EditorGroupPart.RowStart, 0).RemoveTag<TextFieldFocused>());
        AddUntilStep("the typed start is kept", () => Mathf.Abs(Group().Effects[0].Start - 1.5f) < 0.001f);

        // ---------------------------------------------------------------- key lane

        AddStep("click the key on the lane", () => Context.Pointer.Click(LanePin(Group().ShaderKeys[0].Time)));
        AddWaitStep("hold the key", 2);
        AddStep("release the key", () => Context.Pointer.Release());
        AddUntilStep("the key is selected and its cells show", () =>
            State().SelectedKey >= 0f && Visible(EditorGroupPart.KeyValue) && Visible(EditorGroupPart.KeyEase));
        AddStep("focus the key's value", () => Piece(EditorGroupPart.KeyValue).AddTag<TextFieldFocused>());
        AddStep("type an exact value", () =>
            Piece(EditorGroupPart.KeyValue).GetComponent<TextField>().Value = "0.25 0.5 0.75 1");
        AddStep("leave the value", () => Piece(EditorGroupPart.KeyValue).RemoveTag<TextFieldFocused>());
        AddUntilStep("the key takes the typed value", () =>
            Group().ShaderKeys.Count == 1 && Group().ShaderKeys[0].Value == new Vector4(0.25f, 0.5f, 0.75f, 1f));
        AddStep("open the key's ease picker", () => Click(Piece(EditorGroupPart.KeyEase)));
        AddWaitStep("hold EASE", 2);
        AddStep("release EASE", () => Context.Pointer.Release());
        AddUntilStep("the picker opens for the key", () => State().EaseFor == EditorEaseTarget.Key);
        AddStep("pick BOUNCE OUT", () => Click(EaseCell(EasePreset.BounceOut)));
        AddWaitStep("hold the curve", 2);
        AddStep("release the curve", () => Context.Pointer.Release());
        AddUntilStep("the key is eased", () => Group().ShaderKeys[0].Ease == EasePreset.BounceOut);
        AddStep("close the picker", () => Click(EasePickerPanelEntity().GetComponent<EasePicker>().Parts.Close));
        AddWaitStep("hold ×", 2);
        AddStep("release ×", () => Context.Pointer.Release());
        AddUntilStep("the picker is closed", () => State().EaseFor == EditorEaseTarget.None);

        float keyFrom = 0f;
        AddStep("grab the key to drag it", () =>
        {
            keyFrom = Group().ShaderKeys[0].Time;
            Context.Pointer.Click(LanePin(keyFrom));
        });
        AddWaitStep("hold the grab", 2);
        AddStep("drag the key a second earlier", () => Context.Pointer.Position = LanePin(keyFrom - 1f));
        AddWaitStep("drag", 2);
        AddStep("let go of the key", () => Context.Pointer.Release());
        AddUntilStep("the key moved a second earlier", () =>
            Group().ShaderKeys.Count == 1 &&
            Mathf.Abs(Group().ShaderKeys[0].Time - (keyFrom - 1f)) < 0.01f &&
            Mathf.Abs(State().SelectedKey - (keyFrom - 1f)) < 0.01f);

        AddStep("key a second value at the playhead", () => Click(Piece(EditorGroupPart.ShaderUp)));
        AddWaitStep("hold plus again", 2);
        AddStep("release plus again", () => Context.Pointer.Release());
        AddUntilStep("the lane has two keys", () => Group().ShaderKeys.Count == 2);
        AddWaitStep("look at the key lane", 4);

        AddStep("click the first key again", () => Context.Pointer.Click(LanePin(keyFrom - 1f)));
        AddWaitStep("hold the first key", 2);
        AddStep("release the first key", () => Context.Pointer.Release());
        AddUntilStep("the first key is selected", () => Mathf.Abs(State().SelectedKey - (keyFrom - 1f)) < 0.01f);
        AddStep("delete the selected key", () => Click(Piece(EditorGroupPart.KeyDelete)));
        AddWaitStep("hold X", 2);
        AddStep("release X", () => Context.Pointer.Release());
        AddUntilStep("only the other key is left", () =>
            Group().ShaderKeys.Count == 1 && State().SelectedKey < 0f && !Visible(EditorGroupPart.KeyValue));

        // ---------------------------------------------------------------- buffer row and lock

        AddStep("try to buffer the group across two layers", () => Click(Piece(EditorGroupPart.BufferToggle)));
        AddWaitStep("hold BUF", 2);
        AddStep("release BUF", () => Context.Pointer.Release());
        AddUntilStep("buffering is refused with the one-layer rule", () =>
            !Group().Buffered && WarningText() == GroupBufferRules.OneLayer);
        AddStep("dismiss the buffer warning", () => Click(Warning().Button));
        AddWaitStep("hold the warning OK", 2);
        AddStep("release the warning OK", () => Context.Pointer.Release());
        AddUntilStep("the buffer warning closes", () => !WarningShown());

        AddStep("put the block on the group's layer", () =>
            ItemLayer.Put(_block, new ItemLayer(ItemLayer.Of(_note).Layer, ItemLayer.Of(_block).Z)));
        AddStep("buffer the group", () => Click(Piece(EditorGroupPart.BufferToggle)));
        AddWaitStep("hold BUF again", 2);
        AddStep("release BUF again", () => Context.Pointer.Release());
        AddUntilStep("the group is buffered", () => Group().Buffered);

        AddStep("select only the note", () => Editor().GetComponent<NoteSelection>().SetMany(new[] { _note }));

        AddAssert("the buffer settings stay hidden on the ITEM SHADER tab", () => !Visible(EditorGroupPart.BufferTint));
        AddStep("open the BUFFER tab", () => Click(Piece(EditorGroupPart.TrackBuffer)));
        AddWaitStep("hold BUFFER", 2);
        AddStep("release BUFFER", () => Context.Pointer.Release());
        AddUntilStep("the buffer settings show", () => State().BufferTrack && Visible(EditorGroupPart.BufferTint));

        AddStep("open the buffer tint palette", () => Click(Piece(EditorGroupPart.BufferTint)));
        AddWaitStep("hold TINT", 2);
        AddStep("release TINT", () => Context.Pointer.Release());
        AddUntilStep("the palette opens for the buffer, without alpha steps", () =>
            Visible(EditorGroupPart.PaletteSwatch, 3) && !Visible(EditorGroupPart.PaletteAlpha, 0));
        AddStep("pick a swatch", () => Click(Piece(EditorGroupPart.PaletteSwatch, 3)));
        AddWaitStep("hold the swatch", 2);
        AddStep("release the swatch", () => Context.Pointer.Release());
        AddUntilStep("the buffer tint changes and the palette closes", () =>
            Group().BufferTint != Colors.White && !State().PaletteOpen);
        AddStep("drop the buffer alpha", () => Click(Piece(EditorGroupPart.BufferAlphaDown)));
        AddWaitStep("hold A-", 2);
        AddStep("release A-", () => Context.Pointer.Release());
        AddUntilStep("the buffer alpha drops", () => Group().BufferAlpha < 0.95f);
        AddStep("open the buffer blend list", () => Click(Piece(EditorGroupPart.BufferBlend)));
        AddWaitStep("hold BLEND", 2);
        AddStep("release BLEND", () => Context.Pointer.Release());
        AddUntilStep("the blend list is open", () => Piece(EditorGroupPart.BufferBlend).GetComponent<Dropdown>().Open);
        AddStep("choose add", () => ClickOption(EditorGroupPart.BufferBlend, System.Array.IndexOf(BlendModes.All, BlendMode.Add)));
        AddWaitStep("hold the blend option", 2);
        AddStep("release the blend option", () => Context.Pointer.Release());
        AddUntilStep("the buffer blends additively", () => Group().BufferBlend == BlendMode.Add);

        // The panel's scrim keeps presses off the field but not off the
        // sidebar, so the groups grid and layer inspector work with it open.
        AddStep("try to give the note a second group", () => Click(Cell(5)));
        AddWaitStep("hold group 6", 2);
        AddStep("release group 6", () => Context.Pointer.Release());
        AddUntilStep("the second group is refused", () =>
            WarningText() == GroupBufferRules.OneGroup &&
            !(_note.TryGetComponent(out ItemGroups mask) && mask.Has(5)));
        AddStep("dismiss the membership warning", () => Click(Warning().Button));
        AddWaitStep("hold the membership OK", 2);
        AddStep("release the membership OK", () => Context.Pointer.Release());
        AddUntilStep("the membership warning closes", () => !WarningShown());

        AddStep("try to move the note to another layer", () => Click(LayerStep(1)));
        AddWaitStep("hold LAYER +", 2);
        AddStep("release LAYER +", () => Context.Pointer.Release());
        AddUntilStep("the layer move is refused", () =>
            WarningText() == GroupBufferRules.OneLayer &&
            ItemLayer.Of(_note).Layer == -1);
        AddStep("dismiss the layer warning", () => Click(Warning().Button));
        AddWaitStep("hold the layer OK", 2);
        AddStep("release the layer OK", () => Context.Pointer.Release());
        AddUntilStep("the layer warning closes", () => !WarningShown());
        AddAssert("the panel stayed open on group 3 throughout", () => State().Open == 2);

        AddStep("open the BUFFER tab again", () => Click(Piece(EditorGroupPart.TrackBuffer)));
        AddWaitStep("hold BUFFER again", 2);
        AddStep("release BUFFER again", () => Context.Pointer.Release());
        AddUntilStep("the row tracks the buffer", () => State().BufferTrack);

        AddStep("open the buffer's SHD list", () => Click(Piece(EditorGroupPart.ShaderSlot)));
        AddWaitStep("hold the buffer SHD", 2);
        AddStep("release the buffer SHD", () => Context.Pointer.Release());
        AddUntilStep("the buffer's list is open", () => Piece(EditorGroupPart.ShaderSlot).GetComponent<Dropdown>().Open);
        AddStep("give the buffer the test shader", () => ClickShaderOption(1));
        AddWaitStep("hold the buffer option", 2);
        AddStep("release the buffer option", () => Context.Pointer.Release());
        AddUntilStep("the buffer uses slot 1", () => Group().BufferShader == 1);
        AddStep("key a buffer value", () => Click(Piece(EditorGroupPart.ShaderUp)));
        AddWaitStep("hold the buffer plus", 2);
        AddStep("release the buffer plus", () => Context.Pointer.Release());
        AddUntilStep("the buffer track has its own key", () =>
            Group().BufferKeys.Count == 1 && Group().BufferKeys[0].Param == "tint");

        AddStep("put the buffer shader back off for the shot", () =>
        {
            // The test shader writes a solid colour, so the buffer would hide the
            // members. Walk the data back; the track was checked above.
            Group().BufferShader = 0;
            Group().BufferKeys.Clear();
        });
        AddAssert("the buffered group keeps its settings", () =>
            Group().Buffered && Group().BufferShader == 0 && Group().BufferAlpha < 0.95f);
        AddWaitStep("look at the buffer row", 4);

        AddStep("switch back to the ITEM SHADER tab", () => Click(Piece(EditorGroupPart.TrackItem)));
        AddWaitStep("hold ITEM SHADER", 2);
        AddStep("release ITEM SHADER", () => Context.Pointer.Release());
        AddUntilStep("the lane shows the item track's key", () =>
            !State().BufferTrack && Group().ShaderKeys.Count == 1);

        // ---------------------------------------------------------------- cycles

        AddStep("select the move effect", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the move", 2);
        AddStep("release the move", () => Context.Pointer.Release());
        AddUntilStep("the move is selected and plays once", () =>
            State().SelectedEffect == 0 && Group().Effects[0].Cycles == 1 && Visible(EditorGroupPart.CycleCount));
        AddStep("focus the cycle count", () => Piece(EditorGroupPart.CycleCount).AddTag<TextFieldFocused>());
        AddStep("type eight cycles", () => Piece(EditorGroupPart.CycleCount).GetComponent<TextField>().Value = "8");
        AddStep("leave the cycle count", () => Piece(EditorGroupPart.CycleCount).RemoveTag<TextFieldFocused>());
        AddUntilStep("the move repeats eight times, a beat apart", () =>
            Group().Effects[0].Cycles == 8 && Group().Effects[0].EveryBeats == 1f);
        AddStep("space the cycles out", () => Click(Piece(EditorGroupPart.CycleEveryUp)));
        AddWaitStep("hold spacing +", 2);
        AddStep("release spacing +", () => Context.Pointer.Release());
        AddUntilStep("every two beats", () => Group().Effects[0].EveryBeats == 2f);
        AddStep("open the cycle mode list", () => Click(Piece(EditorGroupPart.CycleMode)));
        AddWaitStep("hold the mode", 2);
        AddStep("release the mode", () => Context.Pointer.Release());
        AddUntilStep("the mode list is open", () => Piece(EditorGroupPart.CycleMode).GetComponent<Dropdown>().Open);
        AddStep("choose RETURN", () => ClickOption(EditorGroupPart.CycleMode, 1));
        AddWaitStep("hold RETURN", 2);
        AddStep("release RETURN", () => Context.Pointer.Release());
        AddUntilStep("each cycle goes and comes back", () => Group().Effects[0].Cycle == GroupCycle.Return);
        AddStep("zoom the timeline out to see the repeats", () => Click(Piece(EditorGroupPart.ZoomOut)));
        AddWaitStep("hold zoom out", 2);
        AddStep("release zoom out", () => Context.Pointer.Release());
        AddWaitStep("look at the repeats", 4);

        AddStep("open the grid menu", () => Click(ViewBar().GridButton));
        AddWaitStep("hold the grid button", 2);
        AddStep("release the grid button", () => Context.Pointer.Release());
        AddUntilStep("the grid menu shows", () => ViewBar().Open == EditorViewMenu.Grid && ViewBar().GridMenu.Enabled);
        AddStep("turn grid snap on", () => Click(GridSnapToggle()));
        AddWaitStep("hold grid snap", 2);
        AddStep("release grid snap", () => Context.Pointer.Release());
        AddUntilStep("grid snap is on", () => ViewportSnap.IsOn(Context.Entities));

        AddStep("open the music menu", () => Click(ViewBar().MusicButton));
        AddWaitStep("hold the music button", 2);
        AddStep("release the music button", () => Context.Pointer.Release());
        AddUntilStep("the music menu replaces the grid menu", () =>
            ViewBar().Open == EditorViewMenu.Music && ViewBar().MusicMenu.Enabled && !ViewBar().GridMenu.Enabled);
        AddStep("pick 1/8", () => Click(ViewBar().DivisionButtons[3]));
        AddWaitStep("hold 1/8", 2);
        AddStep("release 1/8", () => Context.Pointer.Release());
        AddUntilStep("the snap is an eighth of a beat", () => Editor().GetComponent<Playhead>().SnapDivision == 8);
        AddStep("turn music snap off", () => Click(MusicSnapToggle()));
        AddWaitStep("hold music snap", 2);
        AddStep("release music snap", () => Context.Pointer.Release());
        AddUntilStep("music snap is off on the playhead", () => Editor().GetComponent<Playhead>().SnapOff);
        AddWaitStep("look at the bar and its menu", 5);
    }

    private Entity Editor() =>
        Context.Entities.Query<EditorGroupState>().ToEntityList()[0];

    private EditorGroupState State() => Editor().GetComponent<EditorGroupState>();

    /// <summary>The middle of an effect's band on the timeline, where a press grabs it whole.</summary>
    private Vector2 Bar(int row)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 origin = EditorGroupLayout.Origin(screen);
        Rect2 timeline = EditorGroupLayout.Timeline(EditorGroupLayout.DialogSize(screen));
        timeline.Position += origin;
        EditorGroupState state = State();
        return EditorGroupLayout.BarRect(Group().Effects[row], row, state.ViewStart, state.ViewSpan, timeline).GetCenter();
    }

    private Vector2 RulerPoint(float seconds)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Vector2 origin = EditorGroupLayout.Origin(screen);
        Rect2 timeline = EditorGroupLayout.Timeline(dialog);
        Rect2 ruler = EditorGroupLayout.Ruler(dialog);
        return origin + new Vector2(
            EditorGroupLayout.XForTime(timeline, 0f, 6f, seconds),
            ruler.Position.Y + ruler.Size.Y / 2);
    }

    private Vector2 TargetHandle()
    {
        EditorGroupState state = State();
        GroupDefinition group = Editor().GetComponent<LevelGroups>().Groups[state.SelectedGroup];
        GroupEffect effect = group.Effects[state.SelectedEffect];
        Entity anchor = EditorGroupTarget.AnchorMember(
            Context.Entities,
            Editor().GetComponent<NoteSelection>(),
            state.SelectedGroup);
        Vector2 basePosition = EditorGroupTarget.TryPosition(anchor, out Vector2 position)
            ? position
            : new Vector2(1000, 1000) / 2f;
        return Editor().GetComponent<PlayfieldView>().PlayfieldToScreen(
            basePosition + effect.Offset);
    }

    private GroupDefinition Group() => Editor().GetComponent<LevelGroups>().Groups[2];

    private EditorGroupBar Bar() => Editor().GetComponent<EditorGroupBar>();

    private EditorViewBar ViewBar() => Editor().GetComponent<EditorViewBar>();

    private Entity GridSnapToggle() =>
        Context.Entities.Query<Toggle>().AllTags(Tags.Get<ViewportSnapToggle>()).WithDisabled().ToEntityList()[0];

    private Entity MusicSnapToggle() =>
        Context.Entities.Query<Toggle>().AllTags(Tags.Get<EditorMusicSnapToggle>()).WithDisabled().ToEntityList()[0];

    private Entity Cell(int index)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupCell>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupCell>().Index == index)
                return entity;
        }

        return default;
    }

    private Entity LayerStep(int arg)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.LayerStep))
        {
            if (button.GetComponent<EditorButton>().Arg == arg)
                return button;
        }

        return default;
    }

    private Entity WarningEntity() =>
        Context.Entities.Query<WarningPopup>().WithDisabled().ToEntityList()[0];

    private WarningPopup Warning() => WarningEntity().GetComponent<WarningPopup>();

    private string WarningText() =>
        Warning().Message.GetComponent<Xanadu.Ui.Ecs.Ui.Text>().Value;

    private bool WarningShown() => !UiLayout.IsHidden(WarningEntity());

    private Entity Piece(EditorGroupPart part, int row = 0)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupPiece>().WithDisabled().ToEntityList())
        {
            EditorGroupPiece piece = entity.GetComponent<EditorGroupPiece>();
            if (piece.Part == part && piece.Row == row)
                return entity;
        }

        return default;
    }

    private bool Visible(EditorGroupPart part, int row = 0) =>
        !UiLayout.IsHidden(Piece(part, row));

    private Entity EasePickerPanelEntity()
    {
        foreach (Entity entity in Context.Entities.Query<EasePicker>().WithDisabled().ToEntityList())
            return entity;
        return default;
    }

    private Entity EaseCell(EasePreset preset)
    {
        foreach (Entity entity in Context.Entities.Query<EasePickerCell>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EasePickerCell>().Preset == preset)
                return entity;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    /// <summary>Where a key's pin sits on the key lane, on screen.</summary>
    private Vector2 LanePin(float seconds)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Rect2 lane = EditorGroupLayout.KeyLane(dialog);
        lane = new Rect2(lane.Position + EditorGroupLayout.Origin(screen), lane.Size);
        EditorGroupState state = State();
        return new Vector2(
            EditorGroupLayout.XForTime(lane, state.ViewStart, state.ViewSpan, seconds),
            lane.Position.Y + lane.Size.Y / 2f);
    }

    private void ClickShaderOption(int slot) => ClickOption(EditorGroupPart.ShaderSlot, slot);

    private void ClickOption(EditorGroupPart part, int slot)
    {
        Entity button = Piece(part);
        foreach (Entity option in Context.Entities.Query<DropdownOption>().ToEntityList())
        {
            DropdownOption data = option.GetComponent<DropdownOption>();
            if (data.Owner.Id == button.Id && data.Value == slot)
            {
                Click(option);
                return;
            }
        }
    }

    private void Seek(long ms)
    {
        foreach (Entity entity in Context.Entities.Query<Playhead, EditorGroupState>().ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.TimeMs = ms;
            playhead.SeekRequestMs = ms;
            playhead.Playing = false;
        }
    }
}
