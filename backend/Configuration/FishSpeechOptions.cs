namespace SwAIvyn.Configuration
{
    /// <summary>
    /// Configuration options for Fish Speech TTS service
    /// </summary>
    public class FishSpeechOptions
    {
        public const string SectionName = "FishSpeech";

        /// <summary>
        /// Base URL for the Fish Speech API
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:8000";

        /// <summary>
        /// Whether to automatically start the Fish Speech API server
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// Path to the Fish Speech model directory
        /// </summary>
        public string ModelPath { get; set; } = "../speech/TTS/openaudio-s1-mini";

        /// <summary>
        /// Timeout for API requests in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to enable GPU acceleration (if available)
        /// </summary>
        public bool EnableGpu { get; set; } = true;

        /// <summary>
        /// Maximum text length for synthesis
        /// </summary>
        public int MaxTextLength { get; set; } = 5000;

        /// <summary>
        /// Whether to pre-generate voice embeddings on startup
        /// </summary>
        public bool PreGenerateEmbeddings { get; set; } = false;

        /// <summary>
        /// Voice cache refresh interval in minutes
        /// </summary>
        public int VoiceCacheRefreshMinutes { get; set; } = 60;
    }
}
