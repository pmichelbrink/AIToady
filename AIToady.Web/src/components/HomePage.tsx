import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import SearchBar from './SearchBar';
import SearchButtons from './SearchButtons';
import { useUser } from '../contexts/UserContext';

export default function HomePage() {
  const [searchQuery, setSearchQuery] = useState('');
  const { userEmail, queriesRemaining, showToast } = useUser();
  const navigate = useNavigate();

  const handleInputChange = (query: string) => {
    setSearchQuery(query);
  };

  const handleQuery = () => {
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
    
    const stored = localStorage.getItem('queriesRemaining');
    if (stored) {
      const newCount = parseInt(stored, 10) - 1;
      localStorage.setItem('queriesRemaining', newCount.toString());
    }
    
    navigate(`/query?q=${encodeURIComponent(searchQuery)}`);
  };

  const handleAskToady = () => handleQuery();
  const handleFeelingFroggy = () => handleQuery();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
      <img src="/toady.png" alt="AI Toady" style={{ height: '200px', objectFit: 'contain', backgroundColor: 'transparent' }} />
      <h1>AI Toady</h1>
      <SearchBar onSearch={handleAskToady} onInputChange={handleInputChange} placeholder="Ask a Toady..." style={{ width: '50%' }} />
      <SearchButtons onAskToady={handleAskToady} onFeelingFroggy={handleFeelingFroggy} />
    </div>
  );
}