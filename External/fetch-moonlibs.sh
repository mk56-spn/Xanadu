#!/usr/bin/env bash
# Downloads Moonside's prebuilt native libraries for MoonWorks into External/moonlibs/lib64.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
dest="$here/moonlibs"
url="https://moonside.games/files/moonlibs.tar.gz"
sha="f4d78bfee67df9f59507670c1a91d6ae1f4ec18e67970c8f8e5c384323406f30"

if [[ -f "$dest/lib64/libSDL3.so.0" ]]; then
    echo "moonlibs already in $dest"
    exit 0
fi

mkdir -p "$dest"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
curl -fsSL -o "$tmp/moonlibs.tar.gz" "$url"
echo "$sha  $tmp/moonlibs.tar.gz" | sha256sum -c -
tar xzf "$tmp/moonlibs.tar.gz" -C "$dest" lib64
echo "moonlibs unpacked to $dest/lib64"
