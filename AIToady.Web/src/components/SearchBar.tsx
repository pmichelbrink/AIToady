import { useState } from 'react';
import './SearchBar.css';

interface SearchBarProps {
  onSearch?: (query: string) => void;
  placeholder?: string;
  style?: React.CSSProperties;
}

export default function SearchBar({ onSearch, placeholder = "Search...", style }: SearchBarProps) {
  const [query, setQuery] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim() && onSearch) {
      onSearch(query.trim());
    }
  };

  return (
    <form className="search-container" onSubmit={handleSubmit} style={style}>
      <div className="search-box">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={placeholder}
          className="search-input"
        />
        <button type="submit" className="search-button">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path
              d="M21 21L16.514 16.506M19 10.5C19 15.194 15.194 19 10.5 19S2 15.194 2 10.5 5.806 2 10.5 2 19 5.806 19 10.5Z"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </div>
    </form>
  );
}