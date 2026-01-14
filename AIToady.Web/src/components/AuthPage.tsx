import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { authService } from '../services/authService';
import type { SignUpData } from '../services/authService';
import './AuthPage.css';

export default function AuthPage() {
  const navigate = useNavigate();
  const [isLogin, setIsLogin] = useState(true);
  const [formData, setFormData] = useState({ email: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [showVerification, setShowVerification] = useState(false);
  const [verificationCode, setVerificationCode] = useState('');
  const [signUpUsername, setSignUpUsername] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setMessage('');

    try {
      if (isLogin) {
        const result = await authService.signIn(formData.email, formData.password);
        if (result.success) {
          localStorage.setItem('userEmail', formData.email);
          navigate('/');
        } else {
          setError(result.error || 'Sign in failed');
        }
      } else {
        const result = await authService.signUp(formData as SignUpData);
        if (result.success) {
          setMessage(result.message || 'Sign up successful!');
          setSignUpUsername(result.username || '');
          setShowVerification(true);
        } else {
          setError(result.error || 'Sign up failed');
        }
      }
    } catch (err) {
      setError('An unexpected error occurred');
    } finally {
      setLoading(false);
    }
  };

  const handleVerification = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setMessage('');

    try {
      const result = await authService.confirmSignUp(signUpUsername, verificationCode);
      if (result.success) {
        localStorage.setItem('userEmail', formData.email);
        navigate('/');
      } else {
        setError(result.error || 'Verification failed');
      }
    } catch (err) {
      setError('An unexpected error occurred');
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  return (
    <>
      <Link to="/" style={{ position: 'absolute', top: '20px', left: '20px', textDecoration: 'none', color: 'inherit', fontSize: '24px', fontWeight: 'bold' }}>
        AI Toady
      </Link>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <div className="auth-form">
          <h2>
            {showVerification ? 'Verify Your Email' : (isLogin ? 'Sign In to AI Toady' : 'Sign Up for AI Toady')}
          </h2>
          {showVerification ? (
            <form onSubmit={handleVerification} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {message && (
                <div style={{ padding: '8px', backgroundColor: '#d4edda', color: '#155724', borderRadius: '4px', fontSize: '14px' }}>
                  {message}
                </div>
              )}
              {error && (
                <div style={{ padding: '8px', backgroundColor: '#f8d7da', color: '#721c24', borderRadius: '4px', fontSize: '14px' }}>
                  {error}
                </div>
              )}
              <p style={{ fontSize: '14px', color: '#666', margin: 0 }}>Enter the verification code sent to {formData.email}</p>
              <input
                type="text"
                placeholder="Verification Code"
                value={verificationCode}
                onChange={(e) => setVerificationCode(e.target.value)}
                className="auth-input"
                required
              />
              <button
                type="submit"
                disabled={loading}
                style={{ 
                  background: loading ? '#ccc' : '#1976d2', 
                  color: 'white', 
                  border: 'none', 
                  padding: '12px', 
                  borderRadius: '4px', 
                  fontSize: '14px', 
                  cursor: loading ? 'not-allowed' : 'pointer',
                  fontWeight: '500'
                }}
              >
                {loading ? 'Verifying...' : 'Verify Email'}
              </button>
            </form>
          ) : (
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            {message && (
              <div style={{ padding: '8px', backgroundColor: '#d4edda', color: '#155724', borderRadius: '4px', fontSize: '14px' }}>
                {message}
              </div>
            )}
            {error && (
              <div style={{ padding: '8px', backgroundColor: '#f8d7da', color: '#721c24', borderRadius: '4px', fontSize: '14px' }}>
                {error}
              </div>
            )}
            <input
              type="email"
              name="email"
              placeholder="Email"
              value={formData.email}
              onChange={handleInputChange}
              className="auth-input"
              required
            />
            <input
              type="password"
              name="password"
              placeholder="Password"
              value={formData.password}
              onChange={handleInputChange}
              className="auth-input"
              required
            />
            <button
              type="submit"
              disabled={loading}
              style={{ 
                background: loading ? '#ccc' : '#1976d2', 
                color: 'white', 
                border: 'none', 
                padding: '12px', 
                borderRadius: '4px', 
                fontSize: '14px', 
                cursor: loading ? 'not-allowed' : 'pointer',
                fontWeight: '500'
              }}
            >
              {loading ? 'Processing...' : (isLogin ? 'Sign In' : 'Sign Up')}
            </button>
            </form>
          )}
          {!showVerification && (
            <p style={{ textAlign: 'center', marginTop: '1rem', fontSize: '14px', color: '#666' }}>
              {isLogin ? "Don't have an account?" : "Already have an account? "}
              <button
                onClick={() => setIsLogin(!isLogin)}
                style={{ background: 'none', border: 'none', color: '#1976d2', cursor: 'pointer', textDecoration: 'underline' }}
              >
                {isLogin ? 'Sign Up' : 'Sign In'}
              </button>
            </p>
          )}
        </div>
      </div>
    </>
  );
}