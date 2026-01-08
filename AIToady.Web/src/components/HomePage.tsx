import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import SearchBar from './SearchBar';
import SearchButtons from './SearchButtons';
import Toast from './Toast';

export default function HomePage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [showToast, setShowToast] = useState(false);
  const navigate = useNavigate();

  const handleInputChange = (query: string) => {
    setSearchQuery(query);
  };

  const handleAskToady = () => {
    if (searchQuery.trim()) {
      navigate(`/query?q=${encodeURIComponent(searchQuery)}`);
    } else {
      setShowToast(true);
    }
  };

  const handleFeelingFroggy = () => {
    if (searchQuery.trim()) {
      navigate(`/query?q=${encodeURIComponent(searchQuery)}`);
    } else {
      setShowToast(true);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
      <img src="/toady.png" alt="AI Toady" style={{ height: '200px', objectFit: 'contain', backgroundColor: 'transparent' }} />
      <h1>AI Toady</h1>
      <SearchBar onSearch={handleAskToady} onInputChange={handleInputChange} placeholder="Ask a Toady..." style={{ width: '50%' }} />
      <SearchButtons onAskToady={handleAskToady} onFeelingFroggy={handleFeelingFroggy} />
      <Toast message="AI Toadies are smart, but they're not mind readers..." show={showToast} onClose={() => setShowToast(false)} />
    </div>
  );
}