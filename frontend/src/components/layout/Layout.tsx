import { useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { AnimatePresence } from 'framer-motion';
import Navigation from './Navigation';

const Layout = () => {
  const location = useLocation();
  
  useEffect(() => {
    // Update page title based on current route
    const path = location.pathname.split('/')[1] || 'chat';
    const title = path.charAt(0).toUpperCase() + path.slice(1).replace('-', ' ');
    document.title = `AI Assistant | ${title}`;
  }, [location]);

  return (
    <div className="min-h-screen flex flex-col">
      <Navigation />
      <main className="flex-grow">
        <AnimatePresence mode="wait">
          <Outlet />
        </AnimatePresence>
      </main>
    </div>
  );
};

export default Layout;