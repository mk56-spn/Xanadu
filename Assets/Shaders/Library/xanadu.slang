// xanadu: every shipped library module at once (`import xanadu;`).
//
// Global: ItemInput and friends (stage), PI, TAU, mod, rotate, random (math),
// the shapes (Circle, Box, …; shape), Bezier (curve), colour and its paint
// (colour, paint). Everything else is under its own name: colours., sdf.,
// anchor., ease., blend., gradient., pattern.
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
