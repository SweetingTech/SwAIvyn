import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Sofa } from 'lucide-react';

export interface RoomItem {
  id: string;
  label: string;
  emoji: string;
  description: string;
}

export const AVAILABLE_ROOM_ITEMS: RoomItem[] = [
  { id: 'plant',  label: 'Plant',    emoji: '🌿', description: 'A small potted plant in the corner.' },
  { id: 'lamp',   label: 'Lamp',     emoji: '🪔', description: 'A warm floor lamp.' },
  { id: 'book',   label: 'Book',     emoji: '📚', description: 'A colorful stack of books.' },
  { id: 'rug',    label: 'Rug',      emoji: '🏠', description: 'A cosy area rug.' },
  { id: 'chair',  label: 'Chair',    emoji: '🪑', description: 'A comfortable chair.' },
];

const STORAGE_KEY = 'swaivyn_room_items';

export function loadRoomItems(): string[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return JSON.parse(raw) as string[];
  } catch {
    // ignore
  }
  return [];
}

export function saveRoomItems(items: string[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    // ignore
  }
}

// ─── Public component ─────────────────────────────────────────────────────────

interface RoomCustomizerProps {
  activeItems: string[];
  onActiveItemsChange: (items: string[]) => void;
}

const RoomCustomizer = ({ activeItems, onActiveItemsChange }: RoomCustomizerProps) => {
  const [isOpen, setIsOpen] = useState(false);

  const toggle = useCallback(
    (id: string) => {
      const next = activeItems.includes(id)
        ? activeItems.filter((i) => i !== id)
        : [...activeItems, id];
      saveRoomItems(next);
      onActiveItemsChange(next);
    },
    [activeItems, onActiveItemsChange],
  );

  return (
    <div className="relative">
      <button
        onClick={() => setIsOpen((v) => !v)}
        className={`p-3 rounded-full shadow-md border flex items-center justify-center transition-all backdrop-blur-md ${
          isOpen
            ? 'bg-primary-600 border-primary-500 text-white'
            : 'bg-gray-800/80 border-gray-700 text-gray-300 hover:bg-gray-700 hover:text-white'
        }`}
        title="Customise room"
        aria-label="Open room customiser"
        aria-expanded={isOpen}
      >
        <Sofa size={20} />
      </button>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            className="absolute bottom-14 right-0 w-60 bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/50 overflow-hidden z-50"
            initial={{ y: 10, opacity: 0, scale: 0.97 }}
            animate={{ y: 0, opacity: 1, scale: 1 }}
            exit={{ y: 10, opacity: 0, scale: 0.97 }}
            transition={{ type: 'spring', damping: 25, stiffness: 350 }}
          >
            <div className="px-4 py-3 border-b border-gray-700 bg-gray-800">
              <h3 className="text-sm font-semibold text-gray-200">Room Items</h3>
              <p className="text-xs text-gray-500 mt-0.5">Toggle decorations in your 3D room</p>
            </div>

            <ul className="py-1">
              {AVAILABLE_ROOM_ITEMS.map((item) => {
                const active = activeItems.includes(item.id);
                return (
                  <li key={item.id}>
                    <button
                      onClick={() => toggle(item.id)}
                      className={`w-full flex items-center gap-3 px-4 py-2.5 text-sm transition-colors ${
                        active
                          ? 'bg-primary-900/40 text-primary-300'
                          : 'text-gray-400 hover:bg-gray-800 hover:text-gray-200'
                      }`}
                    >
                      <span className="text-lg leading-none">{item.emoji}</span>
                      <span className="flex-grow text-left font-medium">{item.label}</span>
                      <span
                        className={`w-4 h-4 rounded-full border transition-colors ${
                          active
                            ? 'bg-primary-500 border-primary-400'
                            : 'border-gray-600'
                        }`}
                      />
                    </button>
                  </li>
                );
              })}
            </ul>

            <div className="px-4 py-2 border-t border-gray-700 text-xs text-gray-600">
              {activeItems.length} / {AVAILABLE_ROOM_ITEMS.length} items active
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
};

export default RoomCustomizer;
