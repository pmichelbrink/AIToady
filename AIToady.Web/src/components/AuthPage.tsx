import { useState } from 'react';
import { Link } from 'react-router-dom';
import './AuthPage.css';

export default function AuthPage() {
  const [isLogin, setIsLogin] = useState(true);

  return (
    <>
      <Link to="/" style={{ position: 'absolute', top: '20px', left: '20px', textDecoration: 'none', color: 'inherit', fontSize: '24px', fontWeight: 'bold' }}>
        AI Toady
      </Link>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <div className="auth-form">
          <h2>
            {isLogin ? 'Sign In to AI Toady' : 'Sign Up for AI Toady'}
          </h2>
          <form style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <input
              type="email"
              placeholder="Email"
              className="auth-input"
            />
            <input
              type="password"
              placeholder="Password"
              className="auth-input"
            />
            <button
              type="submit"
              style={{ 
                background: '#1976d2', 
                color: 'white', 
                border: 'none', 
                padding: '12px', 
                borderRadius: '4px', 
                fontSize: '14px', 
                cursor: 'pointer',
                fontWeight: '500'
              }}
            >
              {isLogin ? 'Sign In' : 'Sign Up'}
            </button>
          </form>
          <p style={{ textAlign: 'center', marginTop: '1rem', fontSize: '14px', color: '#666' }}>
            {isLogin ? "Don't have an account?" : "Already have an account? "}
            <button
              onClick={() => setIsLogin(!isLogin)}
              style={{ background: 'none', border: 'none', color: '#1976d2', cursor: 'pointer', textDecoration: 'underline' }}
            >
              {isLogin ? 'Sign Up' : 'Sign In'}
            </button>
          </p>
        </div>
      </div>
    </>
  );
}