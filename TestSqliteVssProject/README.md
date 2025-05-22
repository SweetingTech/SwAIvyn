# SQLite-VSS Integration Project

This project demonstrates how to integrate the SQLite-VSS extension for vector similarity search capabilities in a .NET Core application.

## Quick Start Guide

### Using Pre-built DLLs (Recommended)

This repository contains pre-built DLLs that you can use directly:

```bash
# Build and run the project with pre-built DLLs
dotnet build
dotnet run
```

This approach uses the DLLs already in the bin/Debug/net8.0 directory, which include:
- sqlite-vss.dll
- sqlite-vector.dll
- faiss.dll

### What the Demo Does

The demo application:
1. Checks that all required DLLs are available
2. Opens an in-memory SQLite database
3. Loads the SQLite-VSS extension
4. Creates a vector index table
5. Inserts test vector data
6. Performs a vector similarity search
7. Displays the search results

## Project Overview

SQLite-VSS (Vector Similarity Search) is an extension for SQLite that adds vector similarity search capabilities to the database. This enables you to store vector embeddings in SQLite and perform efficient similarity searches, which is essential for applications involving AI, machine learning, and semantic search.

## Current Implementation Status

The project currently includes:

- A .NET Core console application that demonstrates SQLite functionality
- A simulation of how SQLite-VSS would work if properly integrated
- Diagnostic checks for required extension files
- Example code for vector similarity search using SQLite-VSS

## Building the SQLite-VSS Extension

To build the SQLite-VSS extension from source:

1. **Prerequisites**:
   - CMake (version 3.10 or higher)
   - Git
   - Visual Studio 2019 or later with C++ workload
   - BLAS libraries (required for vector operations)

2. **Build Process**:
   ```powershell
   # Clone the repository
   git clone --recurse-submodules https://github.com/asg017/sqlite-vss.git
   cd sqlite-vss
   
   # Download SQLite source
   # The script will download SQLite version 3.41.2
   
   # Create build directory
   mkdir build
   cd build
   
   # Configure with CMake
   cmake .. `
       -DCMAKE_BUILD_TYPE=Release `
       -DBUILD_SHARED_LIBS=ON `
       -DSQLITE_SOURCE_DIR=../sqlite-source
   
   # Build the extension
   cmake --build . --config Release
   ```

3. **Required Files**:
   After a successful build, you need three main files:
   - `sqlite-vss.dll`: The main extension DLL
   - `sqlite-vector.dll`: Vector operations library
   - `faiss.dll`: The Facebook AI Similarity Search library used by SQLite-VSS

## Integrating SQLite-VSS with .NET

To use SQLite-VSS in a .NET application:

1. **Copy Extension Files**:
   Copy the built DLLs (`sqlite-vss.dll`, `sqlite-vector.dll`, and `faiss.dll`) to your application directory.

2. **Configure SQLite to Load Extensions**:
   ```csharp
   using Microsoft.Data.Sqlite;
   
   // Enable SQLite extensions
   using var connection = new SqliteConnection("Data Source=mydatabase.db;");
   connection.Open();
   
   // Enable loading extensions
   using (var command = connection.CreateCommand())
   {
       command.CommandText = "PRAGMA enable_load_extension = 1";
       command.ExecuteNonQuery();
   }
   
   // Load the SQLite-VSS extension
   using (var command = connection.CreateCommand())
   {
       command.CommandText = "SELECT load_extension('sqlite-vss')";
       command.ExecuteNonQuery();
   }
   ```

3. **Create Vector Tables**:
   ```csharp
   using (var command = connection.CreateCommand())
   {
       // Create a vector table with 384-dimensional vectors (e.g., for embeddings)
       command.CommandText = @"
           CREATE VIRTUAL TABLE embeddings USING vss0(
               vector(384),
               id INTEGER PRIMARY KEY,
               content TEXT
           )";
       command.ExecuteNonQuery();
   }
   ```

4. **Insert Vectors**:
   ```csharp
   using (var command = connection.CreateCommand())
   {
       command.CommandText = @"
           INSERT INTO embeddings(rowid, vector, content) 
           VALUES (@rowid, @vector, @content)";
           
       command.Parameters.AddWithValue("@rowid", 1);
       command.Parameters.AddWithValue("@vector", "[0.1, 0.2, ..., 0.3]"); // JSON array of floats
       command.Parameters.AddWithValue("@content", "Example document");
       command.ExecuteNonQuery();
   }
   ```

5. **Perform Vector Similarity Searches**:
   ```csharp
   using (var command = connection.CreateCommand())
   {
       command.CommandText = @"
           SELECT rowid, content
           FROM embeddings
           WHERE vss_search(vector, @query_vector)
           LIMIT 5";
           
       command.Parameters.AddWithValue("@query_vector", "[0.2, 0.3, ..., 0.1]"); // JSON array of floats
       
       using var reader = command.ExecuteReader();
       while (reader.Read())
       {
           Console.WriteLine($"ID: {reader.GetInt32(0)}, Content: {reader.GetString(1)}");
       }
   }
   ```

## Troubleshooting SQLite-VSS Build

If you encounter build issues:

1. **BLAS Libraries Missing**:
   - Install OpenBLAS: `vcpkg install openblas:x64-windows`
   - Set environment variable: `set BLAS_LIBRARIES=C:\path\to\openblas.lib`

2. **CMake Configuration Issues**:
   - Make sure you're using the correct SQLite version
   - Check that all submodules are properly initialized
   - Verify paths to dependencies are correct

3. **Runtime Issues**:
   - Ensure all DLLs are in the correct location
   - Verify that SQLite extension loading is enabled
   - Check for missing dependencies with tools like Dependency Walker

## Resources

- [SQLite-VSS GitHub Repository](https://github.com/asg017/sqlite-vss)
- [FAISS Library Documentation](https://github.com/facebookresearch/faiss)
- [SQLite Extensions Documentation](https://www.sqlite.org/loadext.html)
