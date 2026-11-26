// xanadu: every shipped library module at once (`import xanadu;`).
//
// Global: ItemInput and friends (stage), List, Counter and Image (store), PI, TAU, mod, rotate, random (math),
// the shapes (Circle, Box, …; shape; Grid, Stripes, Checker, …; pattern), the
// gradients (Linear, Radial, Conical, Diamond, Spiral, …; gradient), Bezier (curve), colour and its paint
// (colour, paint), clock, Clip and Keys (anim), the noises (Perlin, Simplex, ValueNoise, Worley; noise),
// pictures and their filters (filter). Everything else is under its own name: colours., sdf., anchor.,
// ease., eases., blend., gradient., pattern., sprite., noise., filter., pictures., tonemap., shaping., complex.,
// steps. (Step), looks. and beats. (with the Lightning and Sunburst shapes), 3D from the grid:
// surfaces, Camera, lights. and mesh. (mesh), 3D by marching: Ray, Ball, Span, Bounds, Frustum, Melt, Hit (march),
// 3D scenes of solids recorded in a BUILD pass and shot in TRACE: Solid (Block, Orb, Plane, Pillar, Pill, Hoop, Cone),
// Scene, scene.shot, scene.shoot and scene.shadows (scene), what 3D producers and the lighting share:
// Material, Lighting, Surface, Visibility, Seen, makers and IShadows (light),
// a particle system's shapes, moves and looks under particles. (particles), Field (field: a grid that fades and spreads, a block), and for BUILD bricks, GPU sorts and
// prefix sums under sort. (sort) and a BVH rebuilt every frame, with its walk, under bvh. (bvh).
// Nothing here knows the game: its play state and chart are `import rhythm;`.
module xanadu;

__exported import xanadu.stage;
__exported import xanadu.store;
__exported import xanadu.math;
__exported import xanadu.colour;
__exported import xanadu.shape;
__exported import xanadu.ease;
__exported import xanadu.blend;
__exported import xanadu.pattern;
__exported import xanadu.gradient;
__exported import xanadu.curve;
__exported import xanadu.paint;
__exported import xanadu.sprite;
__exported import xanadu.anim;
__exported import xanadu.noise;
__exported import xanadu.filter;
__exported import xanadu.steps;
__exported import xanadu.looks;
__exported import xanadu.mesh;
__exported import xanadu.march;
__exported import xanadu.light;
__exported import xanadu.scene;
__exported import xanadu.particles;
__exported import xanadu.field;
__exported import xanadu.sort;
__exported import xanadu.bvh;
__exported import xanadu.probe;
