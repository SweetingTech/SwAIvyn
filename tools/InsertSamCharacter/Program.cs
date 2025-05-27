using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using YamlDotNet.Serialization;
using Newtonsoft.Json;

namespace SwAIvyn.Scripts
{    /// <summary>
    /// Script to insert Sam character into the database from YAML file
    /// </summary>
    public class InsertSamCharacter
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Starting Sam Character Insertion Script...");

            try
            {
                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                // Configure services
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(configuration);
                
                // Add Entity Framework
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

                // Add logging
                services.AddLogging(builder => builder.AddConsole());

                var serviceProvider = services.BuildServiceProvider();

                // Get database context
                using var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();                // Check if Sam already exists
                var existingSam = await dbContext.Avatars.FirstOrDefaultAsync(a => a.Name == "Sam");
                if (existingSam != null)
                {
                    Console.WriteLine("⚠️ Sam character already exists in database!");
                    Console.WriteLine($"   ID: {existingSam.Id}");
                    Console.WriteLine($"   SystemPrompt Length: {existingSam.SystemPrompt?.Length ?? 0}");
                    Console.WriteLine("   Skipping insertion.");
                    return;
                }                // Read Sam YAML file
                string yamlPath = Path.GetFullPath(Path.Combine("..", "..", "frontend", "AI", "Sam", "Sam_Character_card.yaml"));
                if (!File.Exists(yamlPath))
                {
                    Console.WriteLine($"❌ Sam YAML file not found at: {yamlPath}");
                    return;
                }

                Console.WriteLine($"📄 Reading Sam YAML from: {yamlPath}");
                string yamlContent = await File.ReadAllTextAsync(yamlPath);

                // Parse YAML
                var deserializer = new DeserializerBuilder().Build();
                dynamic yaml = deserializer.Deserialize<dynamic>(yamlContent);

                Console.WriteLine($"✅ YAML parsed successfully");
                Console.WriteLine($"   Name: {yaml["name"]}");
                Console.WriteLine($"   Description Length: {yaml["description"]?.ToString().Length ?? 0}");

                // Convert YAML to SystemPrompt
                string systemPrompt = ConvertYamlToPrompt(yaml);
                Console.WriteLine($"✅ SystemPrompt generated (Length: {systemPrompt.Length})");

                // Create default user (you may need to adjust this)
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");                // Create AvatarInfo entity
                var samAvatar = new AvatarInfo
                {
                    Id = Guid.NewGuid(),
                    UserId = defaultUserId,
                    Name = yaml["name"]?.ToString() ?? "Sam",
                    Description = yaml["description"]?.ToString() ?? "",
                    Personality = yaml["personality"]?.ToString() ?? "",
                    Scenario = yaml["scenario"]?.ToString() ?? "",
                    FirstMessage = yaml["first_message"]?.ToString() ?? "",
                    MessageExample = yaml["message_example"]?.ToString() ?? "",
                    SystemPrompt = systemPrompt,
                    Tags = JsonConvert.SerializeObject(yaml["tags"] ?? new string[0]),
                    Creator = "Death Stranding Game Series",
                    CreatorNotes = yaml["creator_notes"]?.ToString() ?? "",
                    CharacterVersion = yaml["character_version"]?.ToString() ?? "1.0",
                    Talkativeness = float.Parse(yaml["talkativeness"]?.ToString() ?? "0.5"),
                    IsFavorite = yaml["favorite"]?.ToString() == "true",
                    YamlProfile = yamlContent,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    ImagePath = "",
                    VoiceSettings = "",
                    PostHistoryInstructions = "",
                    AlternateGreetings = "[]",
                    Extensions = "{}"
                };                // Insert into database
                Console.WriteLine("💾 Inserting Sam into database...");
                dbContext.Avatars.Add(samAvatar);
                await dbContext.SaveChangesAsync();

                Console.WriteLine("✅ Sam character inserted successfully!");
                Console.WriteLine($"   ID: {samAvatar.Id}");
                Console.WriteLine($"   SystemPrompt Preview: {systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length))}...");
                Console.WriteLine("");
                Console.WriteLine("🎉 Sam is now ready for testing!");
                Console.WriteLine("   Try sending a message to any conversation and Sam should respond.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error inserting Sam: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Converts YAML character profile to system prompt
        /// (Copied from AiChatService)
        /// </summary>
        private static string ConvertYamlToPrompt(dynamic yaml)
        {
            try
            {
                return $@"
You are roleplaying as the AI character below. Remain in character at all times.

Name: {yaml["name"]}
Description: {yaml["description"]}
Personality: {yaml["personality"]}
Scenario: {yaml["scenario"]}
Talkativeness Level: {yaml["talkativeness"]}

Start conversations with:
{yaml["first_message"]}

Example conversation:
{yaml["message_example"]}
".Trim();
            }            catch (Exception)
            {
                return "You are Sam, the stoic courier from Death Stranding.";
            }
        }
    }
}
