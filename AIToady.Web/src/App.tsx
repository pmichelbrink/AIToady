import SearchBar from './components/SearchBar';
import SearchButtons from './components/SearchButtons';

function App(){
  const handleSearch = (query: string) => {
    console.log('Search query:', query);
  };

  const handleAskToady = () => {
    console.log('Ask a Toady clicked');
  };

  const handleFeelingFroggy = () => {
    console.log('I\'m feeling Froggy clicked');
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
      <img src="/toady.png" alt="AI Toady" style={{ height: '200px', objectFit: 'contain', backgroundColor: 'transparent' }} />
      <h1>AI Toady</h1>
      <SearchBar onSearch={handleSearch} placeholder="Ask a Toady..." style={{ width: '50%' }} />
      <SearchButtons onAskToady={handleAskToady} onFeelingFroggy={handleFeelingFroggy} />
    </div>
  )
}
export default App;