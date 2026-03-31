import { motion, AnimatePresence } from 'framer-motion';

interface VoiceRoomAvatarProps {
  isListening: boolean;
  isSpeaking?: boolean;
  isProcessing?: boolean;
}

const VoiceRoomAvatar = ({ isListening, isSpeaking, isProcessing }: VoiceRoomAvatarProps) => {
  // Visual states
  const isActive = isListening || isSpeaking || isProcessing;
  const ringColor = isListening ? 'border-red-400' : isSpeaking ? 'border-cyan-400' : isProcessing ? 'border-yellow-400' : 'border-gray-500';
  const glowColor = isListening ? 'shadow-red-500/50' : isSpeaking ? 'shadow-cyan-500/50' : isProcessing ? 'shadow-yellow-500/50' : 'shadow-gray-500/20';

  const statusText = isListening
    ? 'Listening to you...'
    : isSpeaking
    ? 'Speaking...'
    : isProcessing
    ? 'Thinking...'
    : 'Tap the mic to start';

  return (
    <div className="flex flex-col items-center justify-center w-full h-full pb-20">
      <motion.div
        className={`relative ${isActive ? glowColor : ''} shadow-2xl rounded-full transition-all duration-700`}
        animate={isActive ? {
          scale: [1, 1.02, 1],
        } : { scale: 1 }}
        transition={{ 
          repeat: isActive ? Infinity : 0,
          duration: 3,
          ease: "easeInOut"
        }}
      >
        {/* Core Avatar Orb */}
        <div className={`w-48 h-48 sm:w-64 sm:h-64 rounded-full bg-gradient-to-br from-gray-800 to-black flex items-center justify-center relative overflow-hidden border-2 ${ringColor} transition-colors duration-500`}>

          {/* Inner pulsating core */}
          <motion.div
            className={`absolute inset-0 rounded-full opacity-30 ${isListening ? 'bg-red-500' : isSpeaking ? 'bg-cyan-500' : isProcessing ? 'bg-yellow-500' : 'bg-gray-600'}`}
            animate={isActive ? { scale: [0.8, 1.1, 0.8], opacity: [0.2, 0.5, 0.2] } : { scale: 0.8, opacity: 0.1 }}
            transition={{ repeat: Infinity, duration: isSpeaking ? 0.5 : isListening ? 1.5 : 2 }}
          />

          {/* Abstract Voice Visualizer (Lines) */}
          <div className="absolute inset-0 flex items-center justify-center gap-1 sm:gap-2 px-8">
            {[1, 2, 3, 4, 5, 6, 7].map((i) => {
              // Generate random heights for the speaking animation
              const baseHeight = i === 4 ? 40 : i === 3 || i === 5 ? 30 : 20;
              const activeHeight = isSpeaking ? (Math.random() * 60 + 20) : isListening ? (Math.random() * 20 + 30) : baseHeight;
              
              return (
                <motion.div
                  key={i}
                  className={`w-2 sm:w-3 rounded-full ${isSpeaking ? 'bg-cyan-400 shadow-[0_0_10px_cyan]' : isListening ? 'bg-red-400' : isProcessing ? 'bg-yellow-400' : 'bg-gray-600'}`}
                  animate={{
                    height: isActive ? [`${baseHeight}px`, `${activeHeight}px`, `${baseHeight}px`] : `${baseHeight}px`,
                  }}
                  transition={{
                    repeat: isActive ? Infinity : 0,
                    duration: isSpeaking ? 0.4 + (i * 0.1) : 1.5,
                    ease: "easeInOut"
                  }}
                />
              )
            })}
          </div>
        </div>

        {/* Outer Ripple Rings */}
        <AnimatePresence>
          {isActive && (
            <>
              <motion.div 
                className={`absolute inset-0 rounded-full border-2 ${ringColor} opacity-50`}
                initial={{ scale: 1, opacity: 0.8 }}
                animate={{ scale: 1.5, opacity: 0 }}
                exit={{ opacity: 0 }}
                transition={{
                  repeat: Infinity,
                  duration: 2,
                  ease: "easeOut",
                }}
              />
              <motion.div 
                className={`absolute inset-0 rounded-full border-2 ${ringColor} opacity-50`}
                initial={{ scale: 1, opacity: 0.8 }}
                animate={{ scale: 1.5, opacity: 0 }}
                exit={{ opacity: 0 }}
                transition={{
                  repeat: Infinity,
                  duration: 2,
                  delay: 1,
                  ease: "easeOut",
                }}
              />
            </>
          )}
        </AnimatePresence>
      </motion.div>
      
      <div className="mt-12 text-center h-16">
        <h2 className="text-2xl font-light tracking-wide text-white">AI Assistant</h2>
        <motion.p
          className={`text-sm mt-2 font-medium tracking-wider uppercase ${isListening ? 'text-red-400' : isSpeaking ? 'text-cyan-400' : isProcessing ? 'text-yellow-400' : 'text-gray-500'}`}
          animate={isActive ? { opacity: [0.5, 1, 0.5] } : { opacity: 0.5 }}
          transition={{ repeat: Infinity, duration: 2 }}
        >
          {statusText}
        </motion.p>
      </div>
    </div>
  );
};

export default VoiceRoomAvatar;
