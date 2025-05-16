import { motion } from 'framer-motion';

interface VoiceRoomAvatarProps {
  isListening: boolean;
}

const VoiceRoomAvatar = ({ isListening }: VoiceRoomAvatarProps) => {
  return (
    <div className="flex flex-col items-center justify-center h-full">
      <motion.div
        className="relative"
        animate={isListening ? {
          scale: [1, 1.05, 1],
        } : {}}
        transition={{ 
          repeat: isListening ? Infinity : 0, 
          duration: 2
        }}
      >
        {/* Avatar Circle */}
        <div className="w-48 h-48 rounded-full bg-primary-100 flex items-center justify-center relative overflow-hidden">
          {/* Simple Avatar Face */}
          <div className="w-full h-full flex items-center justify-center">
            <div className="relative w-32 h-32">
              {/* Circle face */}
              <div className="w-32 h-32 rounded-full bg-primary-200"></div>
              
              {/* Eyes */}
              <motion.div 
                className="absolute top-10 left-8 w-4 h-4 rounded-full bg-gray-800"
                animate={isListening ? { scale: [1, 0.8, 1] } : {}}
                transition={{ repeat: isListening ? Infinity : 0, duration: 1.5 }}
              />
              <motion.div 
                className="absolute top-10 right-8 w-4 h-4 rounded-full bg-gray-800"
                animate={isListening ? { scale: [1, 0.8, 1] } : {}}
                transition={{ repeat: isListening ? Infinity : 0, duration: 1.5 }}
              />
              
              {/* Mouth */}
              <motion.div 
                className="absolute bottom-10 left-1/2 transform -translate-x-1/2 w-16 h-2 rounded-full bg-gray-800"
                animate={isListening ? { 
                  height: ["8px", "4px", "8px"],
                  width: ["40px", "32px", "40px"],
                } : {}}
                transition={{ repeat: isListening ? Infinity : 0, duration: 0.5 }}
              />
            </div>
          </div>
        </div>
        
        {/* Listening indicator rings */}
        {isListening && (
          <>
            <motion.div 
              className="absolute inset-0 rounded-full border-4 border-primary-300 opacity-30"
              animate={{ scale: [1, 1.5], opacity: [0.3, 0] }}
              transition={{ 
                repeat: Infinity, 
                duration: 2,
                ease: "easeOut",
              }}
            />
            <motion.div 
              className="absolute inset-0 rounded-full border-4 border-primary-300 opacity-30"
              animate={{ scale: [1, 1.5], opacity: [0.3, 0] }}
              transition={{ 
                repeat: Infinity, 
                duration: 2,
                delay: 0.5,
                ease: "easeOut", 
              }}
            />
          </>
        )}
      </motion.div>
      
      <div className="mt-6 text-center">
        <h2 className="text-xl font-medium text-gray-800">AI Assistant</h2>
        <p className="text-gray-600 text-sm mt-1">
          {isListening ? 'Listening...' : 'Tap the mic to speak'}
        </p>
      </div>
    </div>
  );
};

export default VoiceRoomAvatar;