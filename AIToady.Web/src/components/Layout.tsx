import { useState, useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { authService } from '../services/authService';
import { userService } from '../services/userService';
import { UserContext } from '../contexts/UserContext';
import Toast from './Toast';

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const isAuthPage = location.pathname === '/auth';
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [queriesRemaining, setQueriesRemaining] = useState<number | null>(() => {
    const stored = localStorage.getItem('queriesRemaining');
    return stored ? parseInt(stored, 10) : null;
  });
  const [showDropdown, setShowDropdown] = useState(false);
  const [showToast, setShowToast] = useState(false);
  const [toastMessage, setToastMessage] = useState('');
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    };

    if (showDropdown) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [showDropdown]);

  useEffect(() => {
    const checkAuthStatus = async () => {
      const result = await authService.getCurrentUser();
      if (result.success && result.user?.signInDetails?.loginId) {
        const email = result.user.signInDetails.loginId;
        setUserEmail(email);
        localStorage.setItem('userEmail', email);
        
        // Get user data from DynamoDB only if not in localStorage
        const storedQueries = localStorage.getItem('queriesRemaining');
        if (!storedQueries) {
          try {
            const userResult = await userService.getUser();
            if (userResult.success && userResult.user) {
              const queries = userResult.user.queriesRemaining;
              setQueriesRemaining(queries);
              localStorage.setItem('queriesRemaining', queries.toString());
              showToastMessage(`Welcome back! You have ${queries} queries remaining.`);
            }
          } catch (error) {
            console.error('Failed to fetch user data:', error);
          }
        }
      } else {
        const storedEmail = localStorage.getItem('userEmail');
        if (storedEmail) {
          localStorage.removeItem('userEmail');
        }
        localStorage.removeItem('queriesRemaining');
        setUserEmail(null);
        setQueriesRemaining(null);
      }
    };
    
    checkAuthStatus();

    // Listen for storage changes to update auth state
    const handleStorageChange = () => {
      checkAuthStatus();
    };
    
    window.addEventListener('storage', handleStorageChange);
    
    return () => {
      window.removeEventListener('storage', handleStorageChange);
    };
  }, [location.pathname]);

  const showToastMessage = (message: string) => {
    setToastMessage(message);
    setShowToast(true);
  };

  const handleSignOut = async () => {
    const result = await authService.signOut();
    if (result.success) {
      localStorage.removeItem('userEmail');
      localStorage.removeItem('queriesRemaining');
      setUserEmail(null);
      setShowDropdown(false);
    }
  };

  return (
    <UserContext.Provider value={{ userEmail, queriesRemaining, showToast: showToastMessage }}>
      <style>{`
        @keyframes dropdownFadeIn {
          from {
            opacity: 0;
            transform: translateY(-10px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
      `}</style>
      <div style={{ position: 'relative', minHeight: '100vh' }}>
      {!isAuthPage && (
        userEmail ? (
          <div ref={dropdownRef} style={{ position: 'absolute', top: '20px', right: '20px' }}>
            <button
              onClick={() => {
                const stored = localStorage.getItem('queriesRemaining');
                if (stored) {
                  setQueriesRemaining(parseInt(stored, 10));
                }
                setShowDropdown(!showDropdown);
              }}
              style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#1976d2', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '16px', fontWeight: 'bold', border: 'none', cursor: 'pointer' }}
            >
              {userEmail.charAt(0).toUpperCase()}
            </button>
            {showDropdown && (
              <div style={{ position: 'absolute', top: '50px', right: '0', backgroundColor: '#2d2d2d', border: '1px solid #555', borderRadius: '4px', boxShadow: '0 2px 8px rgba(0,0,0,0.3)', minWidth: '160px', zIndex: 1000, animation: 'dropdownFadeIn 0.2s ease-out', transformOrigin: 'top right', whiteSpace: 'nowrap' }}>
                <div style={{ padding: '8px 16px', color: '#ccc', fontSize: '12px' }}>
                  Queries remaining: {queriesRemaining ?? 'Loading...'}
                </div>
                <a
                  href="mailto:admin@aitoady.com?subject=Please Sir, I Want Some More Queries"
                  style={{ display: 'block', padding: '0px 16px 8px 16px', borderBottom: '1px solid #555', color: '#1976d2', fontSize: '12px', textDecoration: 'none' }}
                >
                  Request more queries
                </a>
                <div style={{ padding: '8px 16px 1px 16px', color: '#ccc', fontSize: '12px' }}>
                  Signed in as {userEmail}
                </div>
                <button
                  onClick={handleSignOut}
                  style={{ width: '100%', padding: '8px 16px', border: 'none', backgroundColor: 'transparent', color: 'white', textAlign: 'left', cursor: 'pointer', fontSize: '14px' }}
                  onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#404040'}
                  onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                >
                  Sign Out
                </button>
              </div>
            )}
          </div>
        ) : (
          <button 
            style={{ 
              position: 'absolute', 
              top: '20px', 
              right: '20px', 
              background: '#1976d2', 
              color: 'white',
              border: 'none', 
              borderRadius: '4px', 
              padding: '8px 16px', 
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: '500'
            }}
            onClick={() => navigate('/auth')}
          >
            Sign in
          </button>
        )
      )}
        {children}
      </div>
      <Toast message={toastMessage} show={showToast} onClose={() => setShowToast(false)} />
    </UserContext.Provider>
  );
}