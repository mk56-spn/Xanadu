using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// The device objects a fullscreen pass into a sampled colour texture needs:
/// one quad, one RGBA8 target with its framebuffer, and the pipeline. Shared by
/// the RenderingDevice diagnostics; render-thread only, like the device.
/// </summary>
internal sealed class RdFullscreenPass
{
    private RdFullscreenPass(
        Rid vertexBuffer, Rid vertexArray, Rid target, Rid framebuffer, Rid pipeline)
    {
        VertexBuffer = vertexBuffer;
        VertexArray = vertexArray;
        Target = target;
        Framebuffer = framebuffer;
        Pipeline = pipeline;
    }

    public Rid VertexBuffer { get; }
    public Rid VertexArray { get; }
    public Rid Target { get; }
    public Rid Framebuffer { get; }
    public Rid Pipeline { get; }

    /// <summary>Builds the pass for <paramref name="shader"/>, or null if the framebuffer is invalid.</summary>
    public static RdFullscreenPass? Create(RenderingDevice device, Rid shader, int size, string label)
    {
        long vertexFormat = device.VertexFormatCreate(
            new Godot.Collections.Array<RDVertexAttribute>
            {
                new()
                {
                    Location = 0,
                    Format = RenderingDevice.DataFormat.R32G32Sfloat,
                    Offset = 0,
                    Stride = 8,
                },
            });

        float[] corners = { -1f, -1f, 1f, -1f, 1f, 1f, -1f, -1f, 1f, 1f, -1f, 1f };
        var bytes = new byte[corners.Length * sizeof(float)];
        System.Buffer.BlockCopy(corners, 0, bytes, 0, bytes.Length);

        Rid vertexBuffer = device.VertexBufferCreate((uint)bytes.Length, bytes);
        Rid vertexArray = device.VertexArrayCreate(
            6, vertexFormat, new Godot.Collections.Array<Rid> { vertexBuffer });

        // Unorm, not Srgb: the canvas samples these texels as they are, and an
        // Srgb target would gamma-encode the colour on write and wash it out.
        var textureFormat = new RDTextureFormat
        {
            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
            Width = (uint)size,
            Height = (uint)size,
            UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                        RenderingDevice.TextureUsageBits.SamplingBit,
        };
        Rid target = device.TextureCreate(textureFormat, new RDTextureView());

        var attachment = new RDAttachmentFormat
        {
            Format = textureFormat.Format,
            Samples = RenderingDevice.TextureSamples.Samples1,
            UsageFlags = (uint)textureFormat.UsageBits,
        };
        long framebufferFormat = device.FramebufferFormatCreate(
            new Godot.Collections.Array<RDAttachmentFormat> { attachment });
        Rid framebuffer = device.FramebufferCreate(new Godot.Collections.Array<Rid> { target });
        if (!framebuffer.IsValid)
        {
            GD.PushError($"{label}: framebuffer is invalid.");
            FreeIfValid(device, target);
            FreeIfValid(device, vertexArray);
            FreeIfValid(device, vertexBuffer);
            return null;
        }

        var colorBlend = new RDPipelineColorBlendState
        {
            Attachments = new Godot.Collections.Array<RDPipelineColorBlendStateAttachment> { new() },
        };
        Rid pipeline = device.RenderPipelineCreate(
            shader,
            framebufferFormat,
            vertexFormat,
            RenderingDevice.RenderPrimitive.Triangles,
            new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled },
            new RDPipelineMultisampleState(),
            new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false },
            colorBlend);

        return new RdFullscreenPass(vertexBuffer, vertexArray, target, framebuffer, pipeline);
    }

    /// <summary>Starts a draw list on the target, cleared to <paramref name="clear"/>, with the quad bound.</summary>
    public long Begin(RenderingDevice device, Color clear)
    {
        long drawList = device.DrawListBegin(
            Framebuffer, RenderingDevice.DrawFlags.ClearColor0, new[] { clear });
        device.DrawListBindRenderPipeline(drawList, Pipeline);
        device.DrawListBindVertexArray(drawList, VertexArray);
        return drawList;
    }

    /// <summary>Draws the quad and closes the list <see cref="Begin"/> opened.</summary>
    public static void End(RenderingDevice device, long drawList)
    {
        device.DrawListDraw(drawList, false, 1);
        device.DrawListEnd();
    }

    public void Free(RenderingDevice device)
    {
        FreeIfValid(device, Framebuffer);
        FreeIfValid(device, Target);
        FreeIfValid(device, Pipeline);
        FreeIfValid(device, VertexArray);
        FreeIfValid(device, VertexBuffer);
    }

    public static void FreeIfValid(RenderingDevice device, Rid rid)
    {
        if (rid.IsValid)
            device.FreeRid(rid);
    }
}
