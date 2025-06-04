using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller exposing text-to-speech functionality.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TtsController : ControllerBase
    {
        private readonly ITtsService _ttsService;
        private readonly ISimpleLoggerService _logger;

        public TtsController(ITtsService ttsService, ISimpleLoggerService logger)
        {
            _ttsService = ttsService;
            _logger = logger;
        }

        /// <summary>
        /// Synthesizes speech from text.
        /// </summary>
        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] TtsRequest request)
        {
            try
            {
                var audio = await _ttsService.SynthesizeAsync(request.Text, request.VoiceId);
                return File(audio, "audio/mpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to synthesize speech", ex);
                return StatusCode(500, "TTS synthesis failed");
            }
        }
    }

    /// <summary>
    /// Request payload for synthesis.
    /// </summary>
    public class TtsRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? VoiceId { get; set; }
    }
}
