# PowerShell script to build sqlite-vss on Windows without BLAS (uses existing repo)
# This script assumes the sqlite-vss directory already exists

# Set SQLite version (only used if we need to download SQLite source)
$SQLITE_VERSION = "3.41.2"
$SQLITE_VERSION_NUMBER = "3410200"

Write-Host "Building sqlite-vss for SQLite version $SQLITE_VERSION (numeric: $SQLITE_VERSION_NUMBER)"
Write-Host "Using existing sqlite-vss repository..."

# Check if cmake is installed
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    Write-Error "CMake is not installed. Please install CMake."
    exit 1
}

# Verify that the sqlite-vss directory exists
if (-not (Test-Path "sqlite-vss")) {
    Write-Error "sqlite-vss directory not found. This script requires an existing repository."
    exit 1
}
Set-Location sqlite-vss

# Check if SQLite source exists, if not download it
$SQLITE_TARBALL = "sqlite-autoconf-$SQLITE_VERSION_NUMBER.tar.gz"
$SQLITE_URL = "https://www.sqlite.org/2023/$SQLITE_TARBALL"

if (-not (Test-Path "sqlite-source")) {
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
    } else {
        Write-Host "Found SQLite tarball, extracting..."
        tar -xzf $SQLITE_TARBALL
        Rename-Item -Path "sqlite-autoconf-$SQLITE_VERSION_NUMBER" -NewName "sqlite-source"
    }
} else {
    Write-Host "Using existing SQLite source."
}

# Temporarily modify CMakeLists.txt files to remove BLAS dependency
Write-Host "Temporarily modifying CMakeLists.txt files to bypass BLAS requirement..."

# Create a backup of the original CMakeLists.txt file
Copy-Item -Path "CMakeLists.txt" -Destination "CMakeLists.txt.backup"

# Create a backup of the faiss CMakeLists.txt file
Copy-Item -Path "vendor/faiss/faiss/CMakeLists.txt" -Destination "vendor/faiss/faiss/CMakeLists.txt.backup"

# Modify the main CMakeLists.txt file to comment out BLAS and LAPACK requirements
$cmakeContent = Get-Content -Path "CMakeLists.txt" -Raw
$replacement1 = "# find_package(BLAS REQUIRED)`n# Disabled BLAS requirement`nset(BLAS_LIBRARIES `"`")"
$replacement2 = "# find_package(LAPACK REQUIRED)`n# Disabled LAPACK requirement`nset(LAPACK_LIBRARIES `"`")"
$cmakeContent = $cmakeContent -replace "find_package\(BLAS\s+REQUIRED\)", $replacement1
$cmakeContent = $cmakeContent -replace "find_package\(LAPACK\s+REQUIRED\)", $replacement2
$cmakeContent | Set-Content -Path "CMakeLists.txt"

# Modify the faiss CMakeLists.txt file to comment out BLAS and LAPACK requirements
$faissCmakeContent = Get-Content -Path "vendor/faiss/faiss/CMakeLists.txt" -Raw
$faissCmakeContent = $faissCmakeContent -replace "find_package\(BLAS REQUIRED\)", "# find_package(BLAS REQUIRED)`n  set(BLAS_LIBRARIES `"`")"
$faissCmakeContent = $faissCmakeContent -replace "find_package\(LAPACK REQUIRED\)", "# find_package(LAPACK REQUIRED)`n  set(LAPACK_LIBRARIES `"`")"
$faissCmakeContent = $faissCmakeContent -replace "target_link_libraries\(faiss PRIVATE \`\${BLAS_LIBRARIES\}\)", "# target_link_libraries(faiss PRIVATE \`${BLAS_LIBRARIES})"
$faissCmakeContent = $faissCmakeContent -replace "target_link_libraries\(faiss_avx2 PRIVATE \`\${BLAS_LIBRARIES\}\)", "# target_link_libraries(faiss_avx2 PRIVATE \`${BLAS_LIBRARIES})"
$faissCmakeContent = $faissCmakeContent -replace "target_link_libraries\(faiss PRIVATE \`\${LAPACK_LIBRARIES\}\)", "# target_link_libraries(faiss PRIVATE \`${LAPACK_LIBRARIES})"
$faissCmakeContent = $faissCmakeContent -replace "target_link_libraries\(faiss_avx2 PRIVATE \`\${LAPACK_LIBRARIES\}\)", "# target_link_libraries(faiss_avx2 PRIVATE \`${LAPACK_LIBRARIES})"
$faissCmakeContent | Set-Content -Path "vendor/faiss/faiss/CMakeLists.txt"

# Create build directory
Write-Host "Building sqlite-vss..."
if (Test-Path "build") {
    Write-Host "Removing existing build directory..."
    Remove-Item -Recurse -Force "build"
}
New-Item -ItemType Directory -Path "build" | Out-Null
Set-Location "build"

# Configure and build
Write-Host "Configuring with CMake..."

cmake .. `
    -DCMAKE_BUILD_TYPE=Release `
    -DBUILD_SHARED_LIBS=ON `
    -DSQLITE_SOURCE_DIR=../sqlite-source `
    -DFAISS_ENABLE_GPU=OFF `
    -DUSE_SYSTEM_BLAS=OFF

# Build the loadable extension
Write-Host "Building with CMake..."
cmake --build . --config Release

# Find and copy the DLL
Write-Host "Locating and copying the DLLs..."
$DLL_PATHS = Get-ChildItem -Recurse -Filter "*.dll" | Where-Object { $_.Name -like "*vss*" }

if ($DLL_PATHS.Count -eq 0) {
    Write-Error "Could not find the built DLL."
    
    # Restore original CMakeLists.txt
    Set-Location ..
    Copy-Item -Path "CMakeLists.txt.backup" -Destination "CMakeLists.txt" -Force
    Remove-Item -Path "CMakeLists.txt.backup"
    
    exit 1
}

# Copy the main sqlite-vss.dll
Copy-Item $DLL_PATHS[0].FullName -Destination "./sqlite-vss.dll"
Write-Host "Built sqlite-vss.dll successfully at $(Get-Location)\sqlite-vss.dll"

# Also copy the SQLite VSS DLLs to our test project directories
$TEST_PROJECT_PATH = "../../TestSqliteVssProject/bin/Debug/net8.0/"
Write-Host "Copying to test project path: $TEST_PROJECT_PATH"

# Create the directory if it doesn't exist
if (-not (Test-Path $TEST_PROJECT_PATH)) {
    New-Item -ItemType Directory -Force -Path $TEST_PROJECT_PATH | Out-Null
}

$REQUIRED_DLLS = @("sqlite-vss.dll", "sqlite-vector.dll", "faiss.dll")
foreach ($DLL in $REQUIRED_DLLS) {
    $DLL_PATH = Get-ChildItem -Recurse -Filter $DLL | Select-Object -First 1
    if ($DLL_PATH) {
        Copy-Item $DLL_PATH.FullName -Destination "$TEST_PROJECT_PATH"
        Write-Host "Copied $DLL to $TEST_PROJECT_PATH"
    } else {
        Write-Warning "Could not find $DLL"
    }
}

# Also copy to the backend directory
$WINDOWS_PATH = "../../backend/bin/Debug/net8.0/win-x64/"
Write-Host "Copying to Windows path: $WINDOWS_PATH"

# Create the directory if it doesn't exist
if (-not (Test-Path $WINDOWS_PATH)) {
    New-Item -ItemType Directory -Force -Path $WINDOWS_PATH | Out-Null
}

# Copy all DLLs to the backend
foreach ($DLL in $REQUIRED_DLLS) {
    $DLL_PATH = Get-ChildItem -Recurse -Filter $DLL | Select-Object -First 1
    if ($DLL_PATH) {
        Copy-Item $DLL_PATH.FullName -Destination "$WINDOWS_PATH"
        Write-Host "Copied $DLL to $WINDOWS_PATH"
    }
}

# Restore original CMakeLists.txt
Set-Location ..
Copy-Item -Path "CMakeLists.txt.backup" -Destination "CMakeLists.txt" -Force
Remove-Item -Path "CMakeLists.txt.backup"

Write-Host "Done!"

# Return to the original directory
Set-Location ../..
