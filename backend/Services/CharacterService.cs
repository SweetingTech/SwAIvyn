using YamlDotNet.Serialization;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Services
{
    public class CharacterService
    {
        public static string ConvertYamlToPrompt(string yamlProfile)
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var yaml = deserializer.Deserialize<Dictionary<string, object>>(yamlProfile);

                return $@"
You are roleplaying as the AI character below. Remain in character.

Name: {GetValueOrDefault(yaml, "name", "Unknown")}
Description: {GetValueOrDefault(yaml, "description", "")}
Personality: {GetValueOrDefault(yaml, "personality", "")}
Scenario: {GetValueOrDefault(yaml, "scenario", "")}
Talkativeness Level: {GetValueOrDefault(yaml, "talkativeness", "0.5")}

Start the conversation with:
{GetValueOrDefault(yaml, "first_message", "")}

Example:
{GetValueOrDefault(yaml, "message_example", "")}
".Trim();
            }
            catch (Exception ex)
            {
                return $"Error parsing YAML: {ex.Message}";
            }
        }

        private static string GetValueOrDefault(Dictionary<string, object> yaml, string key, string defaultValue)
        {
            if (yaml.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }
    }
}
