#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$SCRIPT_DIR/.."
PROJECT="$ROOT/FezEditor/FezEditor.csproj"
DIST="$ROOT/publish"
VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT")

mkdir -p "$DIST"

TARGETS=("linux-x64" "osx-arm64")
for RID in "${TARGETS[@]}"; do
    echo "Publishing $RID..."
    PUBLISH_DIR="$ROOT/FezEditor/bin/publish/$RID"

    dotnet publish "$PROJECT" -c Release -r "$RID" -o "$PUBLISH_DIR"

    ARCHIVE="$DIST/FEZEditor-$VERSION-$RID.tar.gz"
    tar -czf "$ARCHIVE" --exclude='*.pdb' -C "$PUBLISH_DIR" .
    echo "Created $ARCHIVE"
done

echo "Done!"
