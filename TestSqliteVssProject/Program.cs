using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;  // Add this for Take() method
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Generic;
using SQLitePCL;

class Program
{
    static void Main(string[] args)
    {
        // Initialize SQLitePCL.raw before any SQLite operations
        // This allows extension loading
        SQLitePCL.Batteries_V2.Init();
        raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
        
        Console.WriteLine("SQLite VSS Extension Test - Simulation Mode");
        Console.WriteLine("------------------------------------------");
        
        // Get the current directory where the application is running
        string currentDir = Directory.GetCurrentDirectory();
        Console.WriteLine($"Current Directory: {currentDir}");
        
        // Check if extensions are properly copied
        string[] requiredFiles = { "sqlite-vss.dll", "sqlite-vector.dll", "faiss.dll" };
        foreach (var file in requiredFiles)
        {
            string filePath = Path.Combine(currentDir, file);
            Console.WriteLine($"Required file {file}: {(File.Exists(filePath) ? "Exists" : "Missing")}");
        }

        try
        {
            // Create a SQLite connection
            using (var conn = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"))
            {
                conn.Open();
                Console.WriteLine("SQLite connection opened");
                
                // Get SQLite version
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT sqlite_version()";
                    string version = cmd.ExecuteScalar()?.ToString() ?? "Unknown";
                    Console.WriteLine($"SQLite Version: {version}");
                }
                
                // Create a simple test table
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE test (id INTEGER PRIMARY KEY, name TEXT)";
                    cmd.ExecuteNonQuery();
                    
                    cmd.CommandText = "INSERT INTO test VALUES (1, 'Test Item')";
                    cmd.ExecuteNonQuery();
                    
                    cmd.CommandText = "SELECT * FROM test";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Console.WriteLine($"Test DB working: ID={reader.GetInt32(0)}, Name={reader.GetString(1)}");
                        }
                    }
                }
                
                // Load the SQLite VSS extension
                Console.WriteLine("\nAttempting to load SQLite VSS Extension...");
                try 
                {
                    // Enable SQLite extensions through SQLitePCL.raw
                    // This is the correct way to enable extensions in Microsoft.Data.Sqlite
                    raw.sqlite3_enable_load_extension(conn.Handle, 1);
                    Console.WriteLine("SQLite extensions enabled via raw.sqlite3_enable_load_extension");
                    
                    // Try loading the extension with subtle variations to handle platform differences
                try {
                    // Get absolute path to the extension file - try with full path first
                    string extensionPath = Path.GetFullPath(Path.Combine(currentDir, "sqlite-vss.dll"));
                    
                    using (var cmd = conn.CreateCommand())
                    {
                        // Try with full path - most reliable on Windows
                        cmd.CommandText = $"SELECT load_extension('{extensionPath}')";
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("sqlite-vss extension loaded successfully with explicit .dll path");
                    }
                }
                    catch (Exception ex1) 
                    {
                        Console.WriteLine($"First attempt failed: {ex1.Message}, trying alternative approach...");
                        
                        try {
                            // Try with full path
                            string extensionPath = Path.GetFullPath(Path.Combine(currentDir, "sqlite-vss"));
                            
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = $"SELECT load_extension('{extensionPath}')";
                                cmd.ExecuteNonQuery();
                                Console.WriteLine("sqlite-vss extension loaded successfully with full path");
                            }
                        }
                        catch (Exception ex2) {
                            Console.WriteLine($"Second attempt failed: {ex2.Message}, trying with .dll extension...");
                            
                            try {
                                // Try with explicit .dll extension
                                string extensionPath = Path.GetFullPath(Path.Combine(currentDir, "sqlite-vss.dll"));
                                
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = $"SELECT load_extension('{extensionPath}')";
                                    cmd.ExecuteNonQuery();
                                    Console.WriteLine("sqlite-vss extension loaded successfully with .dll extension");
                                }
                            }
                            catch (Exception ex3) {
                                Console.WriteLine($"Third attempt failed: {ex3.Message}");
                                Console.WriteLine("Could not load the SQLite VSS extension after multiple attempts.");
                                throw; // Rethrow to be caught by the outer catch
                            }
                        }
                    }
                    
                    // Run real SQLite VSS operations
                    RunRealVssOperations(conn);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading or using SQLite VSS extension: {ex.Message}");
                    Console.WriteLine("Running in FULL simulation mode instead...");
                    
                    // Run simulated VSS operations
                    RunSimulatedVssOperations(conn);
                    
                    // Output more detailed exception info for diagnostics
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                }
            }
            
            // Print helpful diagnostic information
            Console.WriteLine("\nDiagnostic Information:");
            Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            string? runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            Console.WriteLine($"Runtime Directory: {runtimeDir ?? "Unknown"}");
            
            Console.WriteLine("\nAction Items for SQLite-VSS integration:");
            Console.WriteLine("1. Successfully build sqlite-vss extension (requires BLAS libraries)");
            Console.WriteLine("2. Copy the extension DLLs to the application directory");
            Console.WriteLine("3. Configure SQLite to load extensions");
            Console.WriteLine("4. Load the sqlite-vss extension at runtime");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine("\nTest completed. Press any key to exit...");
        Console.ReadKey();
    }
    
    // Method to run actual SQLite VSS operations when extension is available
    static void RunRealVssOperations(SqliteConnection conn)
    {
        Console.WriteLine("\nRunning REAL SQLite VSS Operations");
        
        // Create a vector index table
        Console.WriteLine("\nCreating a vector index table");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE VIRTUAL TABLE vector_index USING vss0(vector(3), id INTEGER PRIMARY KEY, content TEXT)";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Vector index table created");
        }
        
        // Store vector data
        var vectors = new List<(int id, float[] vector, string content)>
        {
            (1, new float[] { 1.0f, 2.0f, 3.0f }, "Sample item 1"),
            (2, new float[] { 4.0f, 5.0f, 6.0f }, "Sample item 2"),
            (3, new float[] { 7.0f, 8.0f, 9.0f }, "Sample item 3")
        };
        
        // Insert vectors
        Console.WriteLine("\nInserting test vectors");
        foreach (var item in vectors)
        {
            string vectorJson = JsonSerializer.Serialize(item.vector);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO vector_index(rowid, vector, content) VALUES({item.id}, '{vectorJson}', '{item.content}')";
                cmd.ExecuteNonQuery();
                Console.WriteLine($"Inserted vector ID {item.id}");
            }
        }
        
        // Perform vector search
        Console.WriteLine("\nPerforming a vector search");
        using (var cmd = conn.CreateCommand())
        {
            var queryVector = new float[] { 1.0f, 1.0f, 1.0f };
            string vectorJson = JsonSerializer.Serialize(queryVector);
            cmd.CommandText = $"SELECT rowid, content, distance FROM vector_index WHERE vss_search(vector, '{vectorJson}') LIMIT 2";
            
            using (var reader = cmd.ExecuteReader())
            {
                Console.WriteLine("\nSearch results:");
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string content = reader.GetString(1);
                    float distance = reader.GetFloat(2);
                    Console.WriteLine($"ID: {id}, Content: {content}, Distance: {distance:F2}");
                }
            }
        }
    }
    
    // Method to simulate SQLite VSS operations when extension is not available
    static void RunSimulatedVssOperations(SqliteConnection conn)
    {
        Console.WriteLine("\nRunning SIMULATED SQLite VSS Operations");
        
        // Create a simulated vector table with standard SQLite
        Console.WriteLine("\nCreating a simulated vector table");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE vector_index_sim (id INTEGER PRIMARY KEY, vector_json TEXT, content TEXT)";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Simulated vector table created");
        }
        
        // Store vector data in simulation mode
        var vectors = new List<(int id, float[] vector, string content)>
        {
            (1, new float[] { 1.0f, 2.0f, 3.0f }, "Sample item 1"),
            (2, new float[] { 4.0f, 5.0f, 6.0f }, "Sample item 2"),
            (3, new float[] { 7.0f, 8.0f, 9.0f }, "Sample item 3")
        };
        
        // Insert vectors
        Console.WriteLine("\nInserting test vectors (simulated)");
        foreach (var item in vectors)
        {
            string vectorJson = JsonSerializer.Serialize(item.vector);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO vector_index_sim(id, vector_json, content) VALUES({item.id}, '{vectorJson}', '{item.content}')";
                cmd.ExecuteNonQuery();
                Console.WriteLine($"Inserted simulated vector ID {item.id}");
            }
        }
        
        // Simulate vector search with a direct calculation in C#
        Console.WriteLine("\nPerforming a simulated vector search");
        var queryVector = new float[] { 1.0f, 1.0f, 1.0f };
        
        // Get all vectors from the database
        var items = new List<(int id, float[] vector, string content, float distance)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, vector_json, content FROM vector_index_sim";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string vectorJson = reader.GetString(1);
                    float[]? vector = JsonSerializer.Deserialize<float[]>(vectorJson);
                    string content = reader.GetString(2);
                    
                    if (vector == null)
                    {
                        Console.WriteLine($"Warning: Could not deserialize vector for ID {id}");
                        continue;
                    }
                    
                    // Calculate Euclidean distance manually
                    float distance = 0;
                    for (int i = 0; i < vector.Length; i++)
                    {
                        float diff = vector[i] - queryVector[i];
                        distance += diff * diff;
                    }
                    distance = (float)Math.Sqrt(distance);
                    
                    items.Add((id, vector, content, distance));
                }
            }
        }
        
        // Sort by distance (nearest first)
        items.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        // Output simulated results
        Console.WriteLine("\nSimulated search results:");
        foreach (var item in items.Take(2)) // Limit to 2 results
        {
            Console.WriteLine($"ID: {item.id}, Content: {item.content}, Distance: {item.distance:F2}");
        }
    }
}
