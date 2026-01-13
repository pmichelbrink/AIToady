import { ReactNode } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const isAuthPage = location.pathname === '/auth';

  return (
    <div style={{ position: 'relative', minHeight: '100vh' }}>
      {!isAuthPage && (
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
      )}
      {children}
    </div>
  );
}