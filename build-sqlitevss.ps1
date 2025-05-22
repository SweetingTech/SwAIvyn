# PowerShell script to build sqlite-vss on Windows

# Set SQLite version
$SQLITE_VERSION = "3.41.2"
$SQLITE_VERSION_NUMBER = "3410200"

Write-Host "Building sqlite-vss for SQLite version $SQLITE_VERSION (numeric: $SQLITE_VERSION_NUMBER)"

# Check if git is installed
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git is not installed. Please install Git for Windows."
    exit 1
}

# Check if cmake is installed
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    Write-Error "CMake is not installed. Please install CMake."
    exit 1
}

# Clone the repository with submodules if not already cloned
if (-not (Test-Path "sqlite-vss")) {
    Write-Host "Cloning sqlite-vss repository..."
    git clone --recurse-submodules https://github.com/asg017/sqlite-vss.git
}
Set-Location sqlite-vss

# Download the SQLite source if we don't already have it
$SQLITE_TARBALL = "sqlite-autoconf-$SQLITE_VERSION_NUMBER.tar.gz"
$SQLITE_URL = "https://www.sqlite.org/2023/$SQLITE_TARBALL"

if (-not (Test-Path $SQLITE_TARBALL)) {
    Write-Host "Downloading SQLite source for version $SQLITE_VERSION..."
    try {
        Invoke-WebRequest -Uri $SQLITE_URL -OutFile $SQLITE_TARBALL
    }
    catch {
        # Try 2022 if 2023 fails
        $SQLITE_URL = "https://www.sqlite.org/2022/$SQLITE_TARBALL"
        try {
            Invoke-WebRequest -Uri $SQLITE_URL -OutFile $SQLITE_TARBALL
        }
        catch {
            Write-Error "Could not download SQLite source. Please check the version number."
            exit 1
        }
    }
    
    # Extract the tarball
    Write-Host "Extracting SQLite source..."
    tar -xzf $SQLITE_TARBALL
    Rename-Item -Path "sqlite-autoconf-$SQLITE_VERSION_NUMBER" -NewName "sqlite-source"
}

# Create build directory
Write-Host "Building sqlite-vss..."
if (Test-Path "build") {
    Remove-Item -Recurse -Force "build"
}
New-Item -ItemType Directory -Path "build" | Out-Null
Set-Location "build"

# Configure and build
Write-Host "Configuring with CMake..."
cmake .. `
    -DCMAKE_BUILD_TYPE=Release `
    -DBUILD_SHARED_LIBS=ON `
    -DSQLITE_SOURCE_DIR=../sqlite-source

# Build the loadable extension
Write-Host "Building with CMake..."
cmake --build . --config Release

# Find and copy the DLL
Write-Host "Locating and copying the DLLs..."
$DLL_PATHS = Get-ChildItem -Recurse -Filter "*.dll" | Where-Object { $_.Name -like "*vss*" }

if ($DLL_PATHS.Count -eq 0) {
    Write-Error "Could not find the built DLL."
    exit 1
}

# Copy the main sqlite-vss.dll
Copy-Item $DLL_PATHS[0].FullName -Destination "./sqlite-vss.dll"
Write-Host "Built sqlite-vss.dll successfully at $(Get-Location)\sqlite-vss.dll"

# Copy all necessary DLLs to the backend directory
$WINDOWS_PATH = "..\..\backend\bin\Debug\net8.0\win-x64\"
Write-Host "Copying to Windows path: $WINDOWS_PATH"

# Create the directory if it doesn't exist
if (-not (Test-Path $WINDOWS_PATH)) {
    New-Item -ItemType Directory -Force -Path $WINDOWS_PATH | Out-Null
}

Copy-Item "sqlite-vss.dll" -Destination "$WINDOWS_PATH"

# Copy additional DLLs if they exist
$ADDITIONAL_DLLS = @("libfaiss.dll", "libfaiss_avx2.dll", "vector0.dll")
foreach ($DLL in $ADDITIONAL_DLLS) {
    $DLL_PATH = Get-ChildItem -Recurse -Filter $DLL | Select-Object -First 1
    if ($DLL_PATH) {
        Copy-Item $DLL_PATH.FullName -Destination "$WINDOWS_PATH"
        Write-Host "Copied $DLL to $WINDOWS_PATH"
    }
}

Write-Host "Done!"
