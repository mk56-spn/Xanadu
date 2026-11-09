// Godot's C# structs are written against real_t, single precision unless the
// engine was built with doubles. Ours always are single.
global using real_t = System.Single;
