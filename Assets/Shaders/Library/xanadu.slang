// xanadu: every shipped library module at once (`import xanadu;`).
//
// Global: ItemInput and friends (stage), PI, TAU, mod, rotate, random (math),
// the shapes (Circle, Box, …; shape; Grid, Stripes, Checker, …; pattern), the
// gradients (Linear, Radial, Conical, Diamond, Spiral, …; gradient), Bezier (curve), colour and its paint
// (colour, paint), clock, Clip and Keys (anim). Everything else is under its own
// name: colours., sdf., anchor., ease., eases., blend., gradient., pattern., sprite.,
// and the run being played: player., score., song., contact., events. (play).
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
__exported import xanadu.play;
