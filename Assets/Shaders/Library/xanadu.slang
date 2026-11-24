// xanadu: every shipped library module at once (`import xanadu;`).
//
// Global: ItemInput and friends (stage), PI, TAU, mod, rotate, random (math),
// the shapes (Circle, Box, …; shape; Grid, Stripes, Checker, …; pattern), the
// gradients (Linear, Radial, Conical, Diamond, Spiral, …; gradient), Bezier (curve), colour and its paint
// (colour, paint), clock, Clip and Keys (anim), the noises (Perlin, Simplex, ValueNoise, Worley; noise),
// pictures and their filters (filter). Everything else is under its own name: colours., sdf., anchor.,
// ease., eases., blend., gradient., pattern., sprite., noise., filter., pictures., tonemap., shaping., complex.,
// steps. (Step), looks. and beats. (with the Lightning and Sunburst shapes), 3D from the grid:
// surfaces, Camera, lights. and mesh. (mesh), 3D by marching: Ray, Ball, Span, Melt, Hit (march), and
// a particle system's shapes, moves and looks under particles. (particles).
// Nothing here knows the game: its play state and chart are `import rhythm;`.
module xanadu;

__exported import xanadu.stage;
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
__exported import xanadu.particles;
