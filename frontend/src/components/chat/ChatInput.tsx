import React, { useState, ChangeEvent, FormEvent } from 'react';

interface ChatInputProps {
  onSendMessage: (message: string) => void;
  onFileUpload: (file: File) => void;
}

/**
 * ChatInput component provides an input area with send button and file upload zone.
 * Fully commented and designed for integration with SignalR chat hub.
 */
const ChatInput: React.FC<ChatInputProps> = ({ onSendMessage, onFileUpload }) => {
  // State to hold the current input text
  const [inputText, setInputText] = useState<string>('');

  /**
   * Handles changes in the input text field.
   * @param e Change event
   */
  const handleInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    setInputText(e.target.value);
  };

  /**
   * Handles form submission to send the message.
   * @param e Form event
   */
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (inputText.trim() === '') return;
    onSendMessage(inputText.trim());
    setInputText('');
  };

  /**
   * Handles file selection for upload.
   * @param e Change event
   */
  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      onFileUpload(e.target.files[0]);
      e.target.value = ''; // Reset file input
    }
  };

  return (
    <form onSubmit={handleSubmit} className="p-4 border-t bg-white">
      <div className="flex items-center space-x-2">
        <input
          type="text"
          placeholder="Type your message..."
          value={inputText}
          onChange={handleInputChange}
          className="flex-grow px-4 py-2 border border-gray-300 rounded-full focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
        />
        <label htmlFor="file-upload" className="p-2 text-gray-500 hover:text-primary-500 cursor-pointer">
          <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828L18 9.828m-4-2.828l6 6" />
          </svg>
        </label>
        <input id="file-upload" type="file" className="hidden" onChange={handleFileChange} />
        <button
          type="submit"
          className="p-2 bg-primary-500 text-white rounded-full hover:bg-primary-600 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2"
        >
          Send
        </button>
      </div>
    </form>
  );
};

export default ChatInput;
