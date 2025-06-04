using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for text-to-speech services.
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Synthesizes speech audio from text.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceId">Optional voice ID.</param>
        /// <returns>Audio data bytes.</returns>
        Task<byte[]> SynthesizeAsync(string text, string? voiceId = null);
    }
}
