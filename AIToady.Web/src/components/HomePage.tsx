import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import SearchBar from './SearchBar';
import SearchButtons from './SearchButtons';
import { useUser } from '../contexts/UserContext';
import { queryService } from '../services/queryService';

export default function HomePage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [dots, setDots] = useState('');
  const { userEmail, queriesRemaining, showToast } = useUser();
  const navigate = useNavigate();

  useEffect(() => {
    if (isSubmitting) {
      setDots('');
      const interval = setInterval(() => {
        setDots(prev => prev.length < 3 ? prev + '.' : prev);
      }, 500);
      return () => clearInterval(interval);
    }
  }, [isSubmitting]);

  const handleInputChange = (query: string) => {
    setSearchQuery(query);
  };

  const handleQuery = async () => {
    if (!searchQuery.trim()) {
      showToast("AI Toadies are smart, but they're not mind readers...");
      return;
    }
    
    if (!userEmail) {
      showToast("You have to sign if you want to get a Toady's attention... ");
      return;
    }
    
    if (queriesRemaining === 0) {
      showToast("A journey of a thousand miles must begin with a single step. Your next step is to pay me!");
      return;
    }
    
    setIsSubmitting(true);
    await new Promise(resolve => setTimeout(resolve, 2000)); // Temporary delay to see animation
    const result = await queryService.createQuery(searchQuery);
    if (!result.success) {
      setIsSubmitting(false);
      showToast('Failed to submit query. Please try again.');
      return;
    }
    
    const stored = localStorage.getItem('queriesRemaining');
    if (stored) {
      const newCount = parseInt(stored, 10) - 1;
      localStorage.setItem('queriesRemaining', newCount.toString());
    }
    
    navigate(`/query?q=${encodeURIComponent(searchQuery)}`);
  };

  const handleAskToady = () => !isSubmitting && handleQuery();
  const handleFeelingFroggy = () => !isSubmitting && handleQuery();

  return (
    <>
      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.5; }
        }
      `}</style>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <img 
          src="/toady.png" 
          alt="AI Toady" 
          style={{ 
            height: '200px', 
            objectFit: 'contain', 
            backgroundColor: 'transparent',
            animation: isSubmitting ? 'pulse 2s ease-in-out infinite' : 'none'
          }} 
        />
        <h1>AI Toady</h1>
        <SearchBar onSearch={handleAskToady} onInputChange={handleInputChange} placeholder="Ask a Toady..." style={{ width: '50%' }} />
        <SearchButtons onAskToady={handleAskToady} onFeelingFroggy={handleFeelingFroggy} />
        <div style={{ marginTop: '1rem', height: '21px', color: '#1976d2', fontSize: '14px' }}>
          {isSubmitting && (
            <>
              Sending<span style={{ display: 'inline-block', width: '18px', textAlign: 'left' }}>{dots}</span>
            </>
          )}
        </div>
      </div>
    </>
  );
}