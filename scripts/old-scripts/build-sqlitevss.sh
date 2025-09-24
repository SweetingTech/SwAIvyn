#!/bin/bash
# Script to build sqlite-vss.dll compatible with SQLite 3.41.2

# Set SQLite version
SQLITE_VERSION="3.41.2"
SQLITE_VERSION_NUMBER=3410200

echo "Building sqlite-vss for SQLite version $SQLITE_VERSION (numeric: $SQLITE_VERSION_NUMBER)"

# Install prerequisites
echo "Installing build prerequisites..."
sudo apt update
sudo apt install -y build-essential cmake git libopenblas-dev liblapack-dev

# Clone the repository with submodules
echo "Cloning sqlite-vss repository..."
if [ ! -d "sqlite-vss" ]; then
  git clone --recurse-submodules https://github.com/asg017/sqlite-vss.git
fi
cd sqlite-vss

# Download the exact SQLite source
echo "Downloading SQLite source for version $SQLITE_VERSION..."
SQLITE_TARBALL="sqlite-autoconf-$SQLITE_VERSION_NUMBER.tar.gz"
SQLITE_URL="https://www.sqlite.org/2023/$SQLITE_TARBALL"

# Try 2023 first, then 2022 if that fails
if ! curl -O $SQLITE_URL; then
  SQLITE_URL="https://www.sqlite.org/2022/$SQLITE_TARBALL"
  if ! curl -O $SQLITE_URL; then
    echo "Could not download SQLite source. Please check the version number."
    exit 1
  fi
fi

tar xzf $SQLITE_TARBALL
mv sqlite-autoconf-$SQLITE_VERSION_NUMBER sqlite-source

# Create build directory
echo "Building sqlite-vss..."
rm -rf build
mkdir -p build
cd build

# Configure and build
cmake .. \
  -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_SHARED_LIBS=ON \
  -DSQLITE_SOURCE_DIR=../sqlite-source

# Build the loadable extension
cmake --build . --config Release

# Find and copy the DLL
echo "Locating and copying the DLLs..."
DLL_PATH=$(find . -name "*.so" | grep -i vss)

if [ -z "$DLL_PATH" ]; then
  echo "Could not find the built DLL."
  exit 1
fi

cp $DLL_PATH ./sqlite-vss.dll
echo "Built sqlite-vss.dll successfully at $(pwd)/sqlite-vss.dll"

# Copy all necessary DLLs
WINDOWS_PATH="/mnt/c/Users/djay/Desktop/Projects/SwAIvyn/backend/bin/Debug/net8.0/win-x64/"
echo "Copying to Windows path: $WINDOWS_PATH"
cp sqlite-vss.dll "$WINDOWS_PATH"
cp libfaiss.so "$WINDOWS_PATH/libfaiss.dll"
cp libfaiss_avx2.so "$WINDOWS_PATH/libfaiss_avx2.dll"
cp vector0.so "$WINDOWS_PATH/vector0.dll"
echo "Copied all DLLs to $WINDOWS_PATH"

echo "Done!"
