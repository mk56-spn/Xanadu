using Xanadu.Lab.Experiments.Aero;
using Xanadu.Lab.Experiments.Fluid;
using Xanadu.Lab.Experiments.Moves;
using Xanadu.Lab.Experiments.Notes;
using Xanadu.Lab.Experiments.Particles;
using Xanadu.Lab.Experiments.Pivot;
using Xanadu.Lab.Experiments.Radiance;
using Xanadu.Lab.Experiments.RenderSpike;
using Xanadu.Lab.Experiments.PixelBorder;
using Xanadu.Lab.Experiments.Rts;
using Xanadu.Lab.Experiments.Snow;
using Xanadu.Lab.Experiments.Triangles;

namespace Xanadu.Lab;

/// <summary>Every experiment the lab shows, one tab each, in tab order.</summary>
public static class LabExperiments
{
    public static ILabExperiment[] All() =>
    [
        new DirectionMoveExperiment(),
        new NoteApproachExperiment(NoteApproachStyle.Ring),
        new NoteApproachExperiment(NoteApproachStyle.Diva),
        new NoteApproachExperiment(NoteApproachStyle.DivaLine),
        new NoteApproachExperiment(NoteApproachStyle.Drop),
        new PixelBorderExperiment(
            "BORDER · MATRIX",
            "res://Experiments/PixelBorder/pixel_border_matrix.gdshader",
            [
                "Constant 5px border, no screen uniforms.",
                "Scale comes from CANVAS_MATRIX * MODEL_MATRIX",
                "once per vertex; the fragment stays cheap.",
            ]),
        new PixelBorderExperiment(
            "BORDER · DERIVATIVE",
            "res://Experiments/PixelBorder/pixel_border_derivative.gdshader",
            [
                "Constant 5px border, no screen uniforms.",
                "Pixels per UV from dFdx / dFdy per fragment.",
                "Works on any SDF, but costs per pixel.",
            ]),
        new PivotExperiment(),
        new TriangleExperiment(
            "TRIS · FRAGMENT",
            "res://Experiments/Triangles/triangles_fragment.gdshader",
            geometry: false,
            [
                "One quad; each pixel finds its cell, turns",
                "toward lab_mouse and tests the triangle.",
                "Cost follows pixels, not triangle count.",
            ]),
        new TriangleExperiment(
            "TRIS · GEOMETRY",
            "res://Experiments/Triangles/triangles_geometry.gdshader",
            geometry: true,
            [
                "One triangle array, three vertices each.",
                "vertex() turns them toward lab_mouse.",
                "Cost follows vertices plus covered pixels.",
            ]),
        ParticleExperiment.Drawable(),
        ParticleExperiment.Godot(),
        new RtsExperiment(),
        new SnowExperiment(),
        new FluidExperiment(),
        new RadianceExperiment(),
        new RenderSpikeExperiment(),
        new AeroButtonExperiment(),
    ];
}
