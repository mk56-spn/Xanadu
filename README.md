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

## Playing it

You need a 64-bit Linux or Windows 11 machine with a GPU that supports Vulkan.
Current NVIDIA, AMD and Intel drivers do. You also need the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Linux is where
the game is made and tested; Windows works but has had little play.

On Linux, from the repo folder:

```bash
External/fetch-moonlibs.sh   # once after cloning: downloads SDL3 and friends
./run.sh game                # the game
./run.sh editor              # the level editor
```

On Windows, in PowerShell from the repo folder:

```powershell
External\fetch-moonlibs.ps1   # once after cloning: downloads SDL3 and friends
.\run.ps1 game                # the game
.\run.ps1 editor              # the level editor
```

If PowerShell blocks the scripts, run them as
`powershell -ExecutionPolicy Bypass -File <script> …`.

### First launch

No music or levels come with the game. The songs listed in song select have no
audio, so they can't be played. To add your own, use **ADD SONG…** on the song
timing screen.

Your songs, levels, profiles, replays and settings are kept in
`~/.local/share/xanadu` on Linux and `%APPDATA%\xanadu` on Windows.

## Licence

Built on [MoonWorks](https://github.com/MoonsideGames/MoonWorks) (SDL_GPU),
[Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) and
[Slang](https://shader-slang.org).

Xanadu's code is licensed under the GNU General Public License, version 3 only
(`GPL-3.0-only`); see `LICENSE`. Parts can be offered under MIT on request.
Third-party code, fonts and libraries keep their own licences, listed in
`LICENSES/` and `External/`.
