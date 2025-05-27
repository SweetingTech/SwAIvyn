using System;
using System.IO;
using System.Linq;
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
{
    public class ListCharacters
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("📋 Listing all characters in database...");

            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

                var serviceProvider = services.BuildServiceProvider();
                using var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

                var characters = await dbContext.Avatars
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => new { a.Name, a.Creator, a.CreatedAt, PromptLength = a.SystemPrompt.Length })
                    .ToListAsync();

                Console.WriteLine($"Found {characters.Count} characters:");
                Console.WriteLine();

                foreach (var character in characters)
                {
                    Console.WriteLine($"📌 {character.Name}");
                    Console.WriteLine($"   Creator: {character.Creator}");
                    Console.WriteLine($"   Created: {character.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"   Prompt Length: {character.PromptLength}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
    }
}
