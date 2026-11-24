#!/bin/sh
# Stage only authoritative package roots; never copy an entire ruleset directory.
set -eu
source_root=$1
output_root=$2
mkdir -p "$output_root"
count=0
for package in "$source_root"/*; do
    [ -d "$package" ] || continue
    id=${package##*/}
    case "$id" in
        ''|*[!a-z0-9-]*|-*|*-|*--*) echo "Invalid ruleset directory: $id" >&2; exit 1 ;;
    esac
    # Working-only packages do not become authoritative by being in the source tree.
    [ -d "$package/native" ] || continue
    if [ ! -f "$package/manifest.json" ] || [ -L "$package" ] || [ -L "$package/manifest.json" ] ||
        [ -n "$(find "$package/native" -type l -print)" ]; then
        echo "Invalid authoritative package: $id" >&2
        exit 1
    fi
    mkdir "$output_root/$id"
    cp "$package/manifest.json" "$output_root/$id/manifest.json"
    cp -R "$package/native" "$output_root/$id/native"
    count=$((count + 1))
done
[ "$count" -gt 0 ] || { echo 'No authoritative ruleset packages' >&2; exit 1; }
