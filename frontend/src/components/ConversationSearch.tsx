import React, { useState } from 'react';

interface ConversationSearchProps {
  onSearch: (query: string) => void;
}

/**
 * ConversationSearch component provides a search input for filtering conversations by title or content.
 * Fully commented and designed for integration with backend APIs.
 */
const ConversationSearch: React.FC<ConversationSearchProps> = ({ onSearch }) => {
  const [query, setQuery] = useState('');

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setQuery(e.target.value);
    onSearch(e.target.value);
  };

  return (
    <div className="mb-2">
      <input
        type="text"
        placeholder="Search conversations..."
        value={query}
        onChange={handleChange}
        className="w-full border border-gray-300 rounded px-3 py-2"
      />
    </div>
  );
};

export default ConversationSearch;
