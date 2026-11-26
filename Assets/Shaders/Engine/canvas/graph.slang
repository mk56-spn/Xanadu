// The canvas each frame: changed slots into the trees, every drawn slot placed, then every instance's primitives;
// drawn onto the frame by the pre-pass and the ordered runs, each at the places the host gives it.
void graph()
{
    patchNodes();
    resolve();
    expand();
    prepass(frame);
    ordered(frame);
}
