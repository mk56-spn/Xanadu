// The canvas each frame: changed slots into the trees, every drawn slot placed, then every instance's primitives.
void graph()
{
    patchNodes();
    resolve();
    expand();
}
