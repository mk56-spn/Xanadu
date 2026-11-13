# Xanadu

A 7-key rhythm platformer. Notes come in time with the song, and hitting one
moves the character: four direction keys choose where to go, and three action
keys do the rest. The note sets how strong the movement is, and you choose the
direction by reading the level. Getting through a song means handling several
streams of input at once.

**Status:** pre-alpha. Things break, formats change and old data isn't kept.

## How this is made

I made all the gameplay design decisions and revisions, designed the systems
architecture and drew the vector art. Much of the code in this repo was
written by AI, mainly DeepSeek and Claude, under my direction.

The game's earlier life, three years of mostly hand-written attempts, is at
[Xanadu-Legacy](https://github.com/mk56-spn/Xanadu-Legacy).

## Running it

Linux, .NET 10 SDK (`global.json` pins 10.0.x).

```bash
External/fetch-moonlibs.sh   # once after cloning: the native libraries (SDL3 and friends)
./run.sh                     # a menu of everything that can be launched
./run.sh game                # the game
./run.sh editor              # the level editor
./run.sh labs                # shader playground, shader editor and GPU experiments
```

`./run.sh` lists every target and its flags at the top of the script.

## Where things are

| Folder | What's in it |
| --- | --- |
| `Core`, `UI`, `HostCore` | The engine: maths, the ECS UI, and the MoonWorks host every exe shares |
| `Slang` | The shader layer: compiling effect bodies, and the code editor's completion, lint and the rest. Tested on its own |
| `Game` | The game, its editor and its exe |
| `Tools` | Labs, the animator, the dev tools (the atlas and the frame autopsy) |
| `Tests` | Unit and visual tests |
| `Assets` | Shaders (the Slang library players write against), fonts, icons, audio, particles, the song catalog |
| `External` | Vendored MoonWorks and the fetch scripts for native libraries |
| `Docs` | The design and architecture docs: start with `Docs/design.md` and `Docs/host.md` |

Built on [MoonWorks](https://github.com/MoonsideGames/MoonWorks) (SDL_GPU),
[Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) and
[Slang](https://shader-slang.org). Third-party licences are in `LICENSES/` and
`External/`.

## Licence

Xanadu's code is licensed under the GNU General Public License, version 3 only
(`GPL-3.0-only`); see `LICENSE`. Parts can be offered under MIT on request.
Third-party code, fonts and libraries keep their own licences, listed above.
