# External

Third-party code vendored into the repo. See `docs/moonworks-port.md`.

## MoonWorks

`MoonWorks/` is a trimmed copy of <https://github.com/MoonsideGames/MoonWorks> at
commit `bd841cb2198bed74018eca49960a4a3336ca17fd` (2026-07-17), zlib licence.
Only the C# sources and licences are kept; its submodules are cut down to the
binding files `MoonWorks.csproj` compiles (`lib/*/`).

- **Patch it freely, in place.** It is our copy now. Note each local change in
  the list below so a later update can carry it over.
- **Never send AI-written PRs upstream.** Their `AGENTS.md` / `CLAUDE.md` forbid
  AI-generated *contributions to their repo*; that is all it covers. Using and
  patching this copy locally is fine.

Local changes: none yet.

## moonlibs (native libraries, not in git)

`fetch-moonlibs.sh` downloads Moonside's prebuilt native bundle (SDL3 3.2.17,
FAudio, IRO, Wellspring, dav1dfile, SDL_shadercross and its compilers), checks
its SHA-256 and unpacks the Linux libraries into `moonlibs/lib64/` (gitignored,
~60 MB). `Host/Xanadu.Host.csproj` copies them next to the exe. Run it once
after cloning:

```bash
External/fetch-moonlibs.sh
```

If the hash check fails, upstream replaced the bundle: check it still works,
then update the hash in the script.
