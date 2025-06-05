import React from 'react';

export const Tooltip: React.FC<{ text: string; children: React.ReactNode }> = ({ text, children }) => (
  <span className="relative group">
    {children}
    <span className="absolute z-10 hidden group-hover:block bg-black text-white text-xs rounded py-1 px-2 left-1/2 -translate-x-1/2 mt-2 whitespace-nowrap shadow-lg">
      {text}
    </span>
  </span>
);
