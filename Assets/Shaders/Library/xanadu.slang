// xanadu: every shipped library module at once (`import xanadu;`).
//
// Global: ItemInput and friends (stage), PI, TAU, mod, rotate, random (math),
// the shapes (Circle, Box, …; shape) and Bezier (curve). Everything else is
// under its own name: color., ease., blend., gradient., pattern.
module xanadu;

__exported import xanadu.stage;
__exported import xanadu.math;
__exported import xanadu.color;
__exported import xanadu.shape;
__exported import xanadu.ease;
__exported import xanadu.blend;
__exported import xanadu.pattern;
__exported import xanadu.gradient;
__exported import xanadu.curve;
