import { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { Sparkles, MessageSquare, Headphones, Brain, Puzzle, Settings, BarChart3, User, Bot, Upload, Menu, X } from 'lucide-react';
import { useTranslation } from '../../hooks/useTranslation';
import { useAuth } from '../../contexts/AuthContext';

const Navigation = () => {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

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
            {user?.role === 'admin' && (
              <NavItem to="/admin/users" icon={<User size={18} />} label="Admin" />
            )}
          </nav>

          <div className="flex items-center space-x-3">
            {user && (
              <span className="text-sm text-gray-600 hidden sm:inline">{user.username}</span>
            )}
            <button
              className="px-3 py-1.5 text-sm rounded-md bg-gray-100 hover:bg-gray-200 text-gray-700 hidden md:block"
              onClick={async () => { await logout(); navigate('/'); }}
            >
              Logout
            </button>
            <div className="md:hidden">
              <MobileMenu isOpen={isMobileMenuOpen} setIsOpen={setIsMobileMenuOpen} />
            </div>
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

interface MobileMenuProps {
  isOpen: boolean;
  setIsOpen: (isOpen: boolean) => void;
}

const MobileMenu = ({ isOpen, setIsOpen }: MobileMenuProps) => {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <>
      <div className="flex items-center">
        <button
          className="inline-flex items-center justify-center p-2 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-primary-500"
          aria-expanded={isOpen}
          onClick={() => setIsOpen(!isOpen)}
        >
          <span className="sr-only">{isOpen ? 'Close main menu' : 'Open main menu'}</span>
          {isOpen ? (
            <X className="h-6 w-6" />
          ) : (
            <Menu className="h-6 w-6" />
          )}
        </button>
      </div>

      {/* Mobile menu overlay */}
      {isOpen && (
        <>
          {/* Backdrop */}
          <div 
            className="fixed inset-0 bg-black bg-opacity-50 z-40 md:hidden"
            onClick={() => setIsOpen(false)}
          />
          
          {/* Mobile menu panel */}
          <div className="absolute top-16 left-0 right-0 bg-white shadow-lg border-t border-gray-200 z-50 md:hidden">
            <div className="px-4 py-6 space-y-1">
              <MobileNavItem to="/dashboard" icon={<BarChart3 size={20} />} label={t('navigation.dashboard')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/chat/new" icon={<MessageSquare size={20} />} label={t('navigation.chat')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/voice-room" icon={<Headphones size={20} />} label={t('navigation.voiceRoom')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/memory" icon={<Brain size={20} />} label={t('navigation.memory')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/knowledge-upload" icon={<Upload size={20} />} label="Knowledge" onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/modules" icon={<Puzzle size={20} />} label={t('navigation.modules')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/agents" icon={<Bot size={20} />} label={t('navigation.agents')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/profile" icon={<User size={20} />} label={t('navigation.profile')} onClick={() => setIsOpen(false)} />
              <MobileNavItem to="/settings" icon={<Settings size={20} />} label={t('navigation.settings')} onClick={() => setIsOpen(false)} />
              {user?.role === 'admin' && (
                <MobileNavItem to="/admin/users" icon={<User size={20} />} label="Admin" onClick={() => setIsOpen(false)} />
              )}
              
              {/* Mobile Logout Button */}
              <div className="mt-4 pt-4 border-t border-gray-200">
                <button
                  className="flex items-center w-full px-4 py-3 text-base font-medium text-red-600 hover:bg-red-50 rounded-lg transition-colors duration-200"
                  onClick={async () => { 
                    await logout(); 
                    navigate('/'); 
                    setIsOpen(false); 
                  }}
                >
                  <span className="mr-3">🚪</span>
                  Logout
                </button>
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
};

interface MobileNavItemProps {
  to: string;
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
}

const MobileNavItem = ({ to, icon, label, onClick }: MobileNavItemProps) => {
  return (
    <NavLink
      to={to}
      onClick={onClick}
      className={({ isActive }) =>
        `flex items-center px-4 py-3 text-base font-medium rounded-lg transition-colors duration-200 ${
          isActive
            ? 'bg-primary-50 text-primary-700 border-l-4 border-primary-500'
            : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
        }`
      }
    >
      <span className="mr-3">{icon}</span>
      {label}
    </NavLink>
  );
};

export default Navigation;
