import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
import { Search, Plus, BookOpen, User, Calendar, Globe, Filter } from 'lucide-react';

interface Memory {
  id: string;
  title: string;
  category: string;
  content: string;
  date: string;
  shared: boolean;
  userId: string;
}

const MemoryPage = () => {
  const [memories, setMemories] = useState<Memory[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All');

  useEffect(() => {
    loadMemories();
  }, []);

  const loadMemories = async () => {
    try {
      setLoading(true);

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Load memories from API
      const response = await fetch(`/api/memory/user/${userId}`);
      if (response.ok) {
        const data = await response.json();
        setMemories(data || []);
      } else {
        // No memories found or API error
        setMemories([]);
      }
    } catch (error) {
      console.error('Error loading memories:', error);
      setMemories([]);
    } finally {
      setLoading(false);
    }
  };

  const filteredMemories = memories.filter(memory => {
    const matchesSearch = memory.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         memory.content.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = selectedCategory === 'All' || memory.category === selectedCategory;
    return matchesSearch && matchesCategory;
  });

  const categories = ['All', 'Personal', 'Facts', 'Events', 'Shared'];

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6">
          <div>
            <h1 className="text-2xl font-medium text-gray-800">Memory</h1>
            <p className="text-gray-600">View and manage your AI's memories</p>
          </div>
          <button className="btn btn-primary mt-2 sm:mt-0">
            <Plus size={16} className="mr-1.5" />
            Add Memory
          </button>
        </div>

        <div className="bg-white rounded-lg shadow-soft">
          {/* Search and Filter */}
          <div className="p-4 border-b">
            <div className="flex flex-col sm:flex-row space-y-2 sm:space-y-0 sm:space-x-2">
              <div className="relative flex-grow">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={18} />
                <input
                  type="text"
                  placeholder="Search memories..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10 pr-4 py-2 w-full border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
                />
              </div>
              <button className="btn btn-ghost border border-gray-300">
                <Filter size={16} className="mr-1.5" />
                Filter
              </button>
            </div>
          </div>

          {/* Categories */}
          <div className="p-2 border-b overflow-x-auto">
            <div className="flex space-x-1">
              {categories.map((category) => (
                <button
                  key={category}
                  onClick={() => setSelectedCategory(category)}
                  className={`px-3 py-1.5 text-sm font-medium rounded-md ${
                    selectedCategory === category
                      ? 'bg-primary-50 text-primary-700'
                      : 'text-gray-700 hover:bg-gray-100'
                  }`}
                >
                  {category === 'Personal' && <User size={14} className="inline mr-1" />}
                  {category === 'Facts' && <BookOpen size={14} className="inline mr-1" />}
                  {category === 'Events' && <Calendar size={14} className="inline mr-1" />}
                  {category === 'Shared' && <Globe size={14} className="inline mr-1" />}
                  {category}
                </button>
              ))}
            </div>
          </div>

          {/* Memory List */}
          <div className="divide-y">
            {loading ? (
              <div className="p-8 text-center">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600 mx-auto"></div>
                <p className="mt-2 text-gray-500">Loading memories...</p>
              </div>
            ) : filteredMemories.length === 0 ? (
              <div className="p-8 text-center">
                <BookOpen size={48} className="mx-auto text-gray-400 mb-4" />
                <h3 className="text-lg font-medium text-gray-900 mb-2">No memories found</h3>
                <p className="text-gray-600 mb-4">
                  {searchTerm || selectedCategory !== 'All'
                    ? 'Try adjusting your search or filter criteria.'
                    : 'Start a conversation with your AI to create memories automatically.'}
                </p>
                <button className="btn btn-primary">
                  <Plus size={16} className="mr-1.5" />
                  Add Memory
                </button>
              </div>
            ) : (
              filteredMemories.map((memory) => (
                <MemoryItem
                  key={memory.id}
                  title={memory.title}
                  category={memory.category}
                  icon={getIconForCategory(memory.category)}
                  date={formatDate(memory.date)}
                  shared={memory.shared}
                />
              ))
            )}
          </div>
        </div>
      </div>
    </motion.div>
  );

  function getIconForCategory(category: string) {
    switch (category) {
      case 'Personal':
        return <User size={16} className="text-primary-500" />;
      case 'Facts':
        return <BookOpen size={16} className="text-secondary-500" />;
      case 'Events':
        return <Calendar size={16} className="text-accent-500" />;
      case 'Shared':
        return <Globe size={16} className="text-green-500" />;
      default:
        return <BookOpen size={16} className="text-gray-500" />;
    }
  }

  function formatDate(dateString: string) {
    try {
      const date = new Date(dateString);
      const now = new Date();
      const diffTime = Math.abs(now.getTime() - date.getTime());
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

      if (diffDays === 1) return 'Today';
      if (diffDays === 2) return 'Yesterday';
      if (diffDays <= 7) return `${diffDays - 1} days ago`;
      if (diffDays <= 30) return `${Math.ceil(diffDays / 7)} weeks ago`;
      return date.toLocaleDateString();
    } catch {
      return 'Unknown';
    }
  }
};

interface MemoryItemProps {
  title: string;
  category: string;
  icon: React.ReactNode;
  date: string;
  shared: boolean;
}

const MemoryItem = ({ title, category, icon, date, shared }: MemoryItemProps) => {
  return (
    <div className="p-4 hover:bg-gray-50 transition-colors duration-150">
      <div className="flex items-start">
        <div className="mr-3 mt-0.5">{icon}</div>
        <div className="flex-grow">
          <div className="flex items-center">
            <span className="text-xs font-medium text-gray-500 bg-gray-100 px-2 py-0.5 rounded">
              {category}
            </span>
            {shared && (
              <span className="ml-2 text-xs font-medium text-secondary-600 bg-secondary-50 px-2 py-0.5 rounded flex items-center">
                <Globe size={12} className="mr-1" />
                Shared
              </span>
            )}
            <span className="ml-auto text-xs text-gray-500">{date}</span>
          </div>
          <p className="mt-1 text-gray-800">{title}</p>
          <div className="mt-2 flex space-x-2">
            <button className="text-xs text-gray-500 hover:text-gray-700">Edit</button>
            <button className="text-xs text-gray-500 hover:text-gray-700">Delete</button>
            <button className="text-xs text-gray-500 hover:text-gray-700">
              {shared ? 'Make Private' : 'Share'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MemoryPage;