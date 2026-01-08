import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import HomePage from './components/HomePage';
import QueryPage from './components/QueryPage';

function App(){
  return (
    <Router>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/query" element={<QueryPage />} />
      </Routes>
    </Router>
  )
}
export default App;