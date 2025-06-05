using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DebugTool;

// Simplified Avatar entity for debugging
public class Avatar
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Simplified Conversation entity for debugging
public class Conversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? CharacterId { get; set; }
    public string CharacterSystemPrompt { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

// Simplified DbContext for debugging
public class DebugDbContext : DbContext
{
    public DbSet<Avatar> Avatars { get; set; }
    public DbSet<Conversation> Conversations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=../data/swai-vyn.db");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        using var context = new DebugDbContext();
        
        Console.WriteLine("=== All Characters in Database ===");
        var characters = await context.Avatars
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        Console.WriteLine($"Total characters found: {characters.Count}");
        
        foreach (var character in characters)
        {
            Console.WriteLine($"\nName: {character.Name}");
            Console.WriteLine($"ID: {character.Id}");
            Console.WriteLine($"Created: {character.CreatedAt}");
            
            if (!string.IsNullOrEmpty(character.SystemPrompt))
            {
                var preview = character.SystemPrompt.Length > 150 
                    ? character.SystemPrompt.Substring(0, 150) + "..."
                    : character.SystemPrompt;
                Console.WriteLine($"System Prompt Preview: {preview}");
            }
            else
            {
                Console.WriteLine("System Prompt: [NULL or EMPTY]");
            }
            Console.WriteLine("---");
        }

        // Test the default character lookup (mimicking DefaultCharacterService)
        Console.WriteLine("\n=== Testing Default Character Lookup ===");
        var gladosDefault = await context.Avatars
            .FirstOrDefaultAsync(a => a.Name == "GLaDOS");
            
        if (gladosDefault != null)
        {
            Console.WriteLine($"GLaDOS Found: {gladosDefault.Name} (ID: {gladosDefault.Id})");
            Console.WriteLine($"Created: {gladosDefault.CreatedAt}");
            
            if (!string.IsNullOrEmpty(gladosDefault.SystemPrompt))
            {
                var preview = gladosDefault.SystemPrompt.Length > 200 
                    ? gladosDefault.SystemPrompt.Substring(0, 200) + "..."
                    : gladosDefault.SystemPrompt;
                Console.WriteLine($"System Prompt Preview: {preview}");
            }
            else
            {
                Console.WriteLine("System Prompt: [NULL or EMPTY]");
            }
        }
        else
        {
            Console.WriteLine("GLaDOS NOT FOUND!");
        }        // Check all conversations to see if any have character context set
        Console.WriteLine("\n=== All Conversations ===");
        var conversations = await context.Conversations
            .OrderBy(c => c.CreatedUtc)
            .ToListAsync();

        Console.WriteLine($"Total conversations found: {conversations.Count}");
        
        foreach (var conversation in conversations)
        {
            Console.WriteLine($"\nTitle: {conversation.Title}");
            Console.WriteLine($"ID: {conversation.Id}");
            Console.WriteLine($"Created: {conversation.CreatedUtc}");
            Console.WriteLine($"CharacterId: {conversation.CharacterId}");
            
            if (!string.IsNullOrEmpty(conversation.CharacterSystemPrompt))
            {
                var preview = conversation.CharacterSystemPrompt.Length > 200 
                    ? conversation.CharacterSystemPrompt.Substring(0, 200) + "..."
                    : conversation.CharacterSystemPrompt;
                Console.WriteLine($"Character System Prompt: {preview}");
                
                // Try to identify which character this is
                if (conversation.CharacterSystemPrompt.Contains("GLaDOS"))
                    Console.WriteLine(">>> This appears to be GLaDOS");
                else if (conversation.CharacterSystemPrompt.Contains("Sam"))
                    Console.WriteLine(">>> This appears to be Sam");
                else if (conversation.CharacterSystemPrompt.Contains("Sherlock"))
                    Console.WriteLine(">>> This appears to be Sherlock");
                else
                    Console.WriteLine(">>> Character not identified");
            }
            else
            {
                Console.WriteLine("Character System Prompt: [NULL or EMPTY - should use GLaDOS default]");
            }
            Console.WriteLine("---");
        }
    }
}
