import { NavLink } from 'react-router-dom';
import { Sparkles, MessageSquare, Headphones, Brain, Puzzle, Settings, BarChart3, User, Bot, Upload } from 'lucide-react';
import { useTranslation } from '../../hooks/useTranslation';

const Navigation = () => {
  const { t } = useTranslation();

  return (
    <header className="sticky top-0 bg-white shadow-sm z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          <div className="flex items-center">
            <NavLink
              to="/"
              className="flex items-center text-primary-600 hover:text-primary-700"
            >
              <Sparkles className="h-6 w-6 mr-2" />
              <span className="text-xl font-medium">AI Assistant</span>
            </NavLink>
          </div>

          <nav className="hidden md:flex space-x-1">
            <NavItem to="/dashboard" icon={<BarChart3 size={18} />} label={t('navigation.dashboard')} />
            <NavItem to="/chat/new" icon={<MessageSquare size={18} />} label={t('navigation.chat')} />
            <NavItem to="/voice-room" icon={<Headphones size={18} />} label={t('navigation.voiceRoom')} />
            <NavItem to="/memory" icon={<Brain size={18} />} label={t('navigation.memory')} />
            <NavItem to="/knowledge-upload" icon={<Upload size={18} />} label="Knowledge" />
            <NavItem to="/modules" icon={<Puzzle size={18} />} label={t('navigation.modules')} />
            <NavItem to="/agents" icon={<Bot size={18} />} label={t('navigation.agents')} />
            <NavItem to="/profile" icon={<User size={18} />} label={t('navigation.profile')} />
            <NavItem to="/settings" icon={<Settings size={18} />} label={t('navigation.settings')} />
          </nav>

          <div className="flex md:hidden">
            <MobileMenu />
          </div>
        </div>
      </div>
    </header>
  );
};

interface NavItemProps {
  to: string;
  icon: React.ReactNode;
  label: string;
}

const NavItem = ({ to, icon, label }: NavItemProps) => {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `px-3 py-2 rounded-md text-sm font-medium transition-colors duration-200 flex items-center ${
          isActive
            ? 'bg-primary-50 text-primary-700'
            : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
        }`
      }
    >
      <span className="mr-1.5">{icon}</span>
      {label}
    </NavLink>
  );
};

const MobileMenu = () => {
  return (
    <div className="flex items-center">
      <button
        className="inline-flex items-center justify-center p-2 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-primary-500"
        aria-expanded="false"
      >
        <span className="sr-only">Open main menu</span>
        <svg
          className="block h-6 w-6"
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          aria-hidden="true"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>
    </div>
  );
};

export default Navigation;