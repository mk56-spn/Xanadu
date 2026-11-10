#!/usr/bin/env bash
# Downloads the Khronos Vulkan headers the GPU timing layer (HostCore/Native) compiles against
# into External/vulkan-headers/include (gitignored).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
dest="$here/vulkan-headers"
version="1.4.368"
url="https://github.com/KhronosGroup/Vulkan-Headers/archive/refs/tags/v$version.tar.gz"
sha="5c5c6f64fcce28a24cc934143a048707af036a858e4ed11d7f71fe016006ecda"

if [[ -f "$dest/include/vulkan/vk_layer.h" && -d "$dest/include/vk_video" ]]; then
    echo "Vulkan headers already in $dest"
    exit 0
fi

mkdir -p "$dest"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
curl -fsSL -o "$tmp/headers.tar.gz" "$url"
echo "$sha  $tmp/headers.tar.gz" | sha256sum -c -
tar xzf "$tmp/headers.tar.gz" -C "$tmp"
mkdir -p "$dest/include/vulkan"
for file in vk_layer.h vk_platform.h vulkan.h vulkan_core.h; do
    cp "$tmp/Vulkan-Headers-$version/include/vulkan/$file" "$dest/include/vulkan/"
done
cp -r "$tmp/Vulkan-Headers-$version/include/vk_video" "$dest/include/"
cp "$tmp/Vulkan-Headers-$version/LICENSE.md" "$dest/"
echo "Vulkan headers $version in $dest"
