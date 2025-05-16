import { motion } from 'framer-motion';
import { Search, Plus, BookOpen, User, Calendar, Globe, Filter } from 'lucide-react';

const MemoryPage = () => {
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
              <button className="px-3 py-1.5 text-sm font-medium bg-primary-50 text-primary-700 rounded-md">
                All
              </button>
              <button className="px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-md">
                <User size={14} className="inline mr-1" />
                Personal
              </button>
              <button className="px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-md">
                <BookOpen size={14} className="inline mr-1" />
                Facts
              </button>
              <button className="px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-md">
                <Calendar size={14} className="inline mr-1" />
                Events
              </button>
              <button className="px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-md">
                <Globe size={14} className="inline mr-1" />
                Shared
              </button>
            </div>
          </div>
          
          {/* Memory List */}
          <div className="divide-y">
            <MemoryItem 
              title="User likes hiking in the mountains"
              category="Personal"
              icon={<User size={16} className="text-primary-500" />}
              date="Today"
              shared={false}
            />
            
            <MemoryItem 
              title="Meeting scheduled for Friday at 3pm"
              category="Event"
              icon={<Calendar size={16} className="text-accent-500" />}
              date="Yesterday"
              shared={true}
            />
            
            <MemoryItem 
              title="User's favorite book is 'Dune' by Frank Herbert"
              category="Fact"
              icon={<BookOpen size={16} className="text-secondary-500" />}
              date="Last week"
              shared={false}
            />
            
            <MemoryItem 
              title="User prefers dark mode in applications"
              category="Preference"
              icon={<User size={16} className="text-primary-500" />}
              date="Last month"
              shared={false}
            />
          </div>
        </div>
      </div>
    </motion.div>
  );
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