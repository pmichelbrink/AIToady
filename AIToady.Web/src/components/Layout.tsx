import { useState, useEffect } from 'react';
import type { ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { authService } from '../services/authService';

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const isAuthPage = location.pathname === '/auth';
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [showDropdown, setShowDropdown] = useState(false);

  useEffect(() => {
    const checkAuthStatus = async () => {
      const result = await authService.getCurrentUser();
      if (result.success && result.user?.signInDetails?.loginId) {
        setUserEmail(result.user.signInDetails.loginId);
        localStorage.setItem('userEmail', result.user.signInDetails.loginId);
      } else {
        const storedEmail = localStorage.getItem('userEmail');
        if (storedEmail) {
          localStorage.removeItem('userEmail');
        }
        setUserEmail(null);
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

  const handleSignOut = async () => {
    const result = await authService.signOut();
    if (result.success) {
      localStorage.removeItem('userEmail');
      setUserEmail(null);
      setShowDropdown(false);
    }
  };

  return (
    <div style={{ position: 'relative', minHeight: '100vh' }}>
      {!isAuthPage && (
        userEmail ? (
          <div style={{ position: 'absolute', top: '20px', right: '20px' }}>
            <button
              onClick={() => setShowDropdown(!showDropdown)}
              style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#1976d2', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '16px', fontWeight: 'bold', border: 'none', cursor: 'pointer' }}
            >
              {userEmail.charAt(0).toUpperCase()}
            </button>
            {showDropdown && (
              <div style={{ position: 'absolute', top: '50px', right: '0', backgroundColor: '#2d2d2d', border: '1px solid #555', borderRadius: '4px', boxShadow: '0 2px 8px rgba(0,0,0,0.3)', minWidth: '120px', zIndex: 1000 }}>
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
  );
}