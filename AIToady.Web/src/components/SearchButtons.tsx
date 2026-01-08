import './SearchButtons.css';

interface SearchButtonsProps {
  onAskToady?: () => void;
  onFeelingFroggy?: () => void;
}

export default function SearchButtons({ onAskToady, onFeelingFroggy }: SearchButtonsProps) {
  return (
    <div className="search-buttons">
      <button className="search-btn" onClick={onAskToady}>
        Ask a Toady
      </button>
      <button className="search-btn" onClick={onFeelingFroggy}>
        I'm feeling Froggy
      </button>
    </div>
  );
}