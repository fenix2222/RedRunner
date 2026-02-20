#!/bin/bash
# Run this after each Unity Web build to fix paths and decompress files
# Usage: ./fix-web-build.sh

BUILD_DIR="Web/Build"

# Decompress .gz files
cd "$(dirname "$0")"
echo "Decompressing build files..."
gunzip -kf "$BUILD_DIR/Web.data.gz" 2>/dev/null
gunzip -kf "$BUILD_DIR/Web.framework.js.gz" 2>/dev/null
gunzip -kf "$BUILD_DIR/Web.wasm.gz" 2>/dev/null

# Fix index.html paths to point to Build/ and use decompressed files
sed -i '' 's|"Web\.loader\.js"|"Build/Web.loader.js"|g' Web/index.html
sed -i '' 's|"Web\.data\.gz"|"Build/Web.data"|g' Web/index.html
sed -i '' 's|"Web\.framework\.js\.gz"|"Build/Web.framework.js"|g' Web/index.html
sed -i '' 's|"Web\.wasm\.gz"|"Build/Web.wasm"|g' Web/index.html
sed -i '' 's|"Web\.data"|"Build/Web.data"|g' Web/index.html
sed -i '' 's|"Web\.framework\.js"|"Build/Web.framework.js"|g' Web/index.html
sed -i '' 's|"Web\.wasm"|"Build/Web.wasm"|g' Web/index.html

echo "Done! Serve with: npx http-server Web -p 8081"
