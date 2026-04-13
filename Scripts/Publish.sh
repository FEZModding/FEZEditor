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

    if [[ "$RID" == osx-* ]]; then
        APP_DIR="$PUBLISH_DIR/FEZEditor.app"
        CONTENTS="$APP_DIR/Contents"
        mkdir -p "$CONTENTS/MacOS"
        mkdir -p "$CONTENTS/Resources"

        # Move all published files into the bundle
        find "$PUBLISH_DIR" -maxdepth 1 -mindepth 1 ! -name "FEZEditor.app" -exec mv {} "$CONTENTS/MacOS/" \;

        # Copy Info.plist with version substituted
        sed "s/\$(Version)/$VERSION/g" "$ROOT/FezEditor/Info.plist" > "$CONTENTS/Info.plist"

        ARCHIVE="$DIST/FEZEditor-$VERSION-$RID.tar.gz"
        tar -czf "$ARCHIVE" --exclude='*.pdb' -C "$PUBLISH_DIR" FEZEditor.app
    else
        ARCHIVE="$DIST/FEZEditor-$VERSION-$RID.tar.gz"
        tar -czf "$ARCHIVE" --exclude='*.pdb' -C "$PUBLISH_DIR" .
    fi

    echo "Created $ARCHIVE"
done

echo "Done!"
