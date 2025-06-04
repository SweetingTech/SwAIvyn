using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for text-to-speech operations.
    /// </summary>
    [ApiController]
    [Route("api/tts")]
    public class TtsController : ControllerBase
    {
        private readonly ITtsService _ttsService;

        public TtsController(ITtsService ttsService)
        {
            _ttsService = ttsService;
        }

        /// <summary>
        /// Synthesizes speech audio from text.
        /// </summary>
        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] TtsRequest request)
        {
            var audio = await _ttsService.SynthesizeAsync(request.Text, request.VoiceId);
            return File(audio, "audio/mpeg");
        }
    }

    /// <summary>
    /// Request model for TTS synthesis.
    /// </summary>
    public class TtsRequest
    {
        /// <summary>The text to synthesize.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Optional voice ID to use.</summary>
        public string? VoiceId { get; set; }
    }
}
