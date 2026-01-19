import { useSearchParams, Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { useUser } from '../contexts/UserContext';
import { queryService } from '../services/queryService';

export default function QueryPage() {
  const [searchParams] = useSearchParams();
  const query = searchParams.get('q') || '';
  const queryId = searchParams.get('id') || '';
  const { showToast, userEmail } = useUser();
  const [messageIndex, setMessageIndex] = useState(0);
  const [showEmailOption, setShowEmailOption] = useState(false);
  const [emailRequested, setEmailRequested] = useState(false);

  const messages = [
    `So you want to know about ${query}?`,
    'Sending your question to a Toady...',
    'Finding the right Toady for the job...',
    'Waiting on a response from your Toady...'
  ];

  useEffect(() => {
    const stored = localStorage.getItem('queriesRemaining');
    if (stored) {
      const remaining = parseInt(stored, 10);
      showToast(`You have ${remaining} queries remaining.`);
    }
  }, []);

  useEffect(() => {
    if (messageIndex < messages.length - 1) {
      const timeout = setTimeout(() => {
        setMessageIndex((prev) => prev + 1);
      }, 5000);

      return () => clearTimeout(timeout);
    } else {
      // All messages shown, wait for progress bar to fill (30s total)
      const timeout = setTimeout(() => {
        setShowEmailOption(true);
      }, 30000 - (messages.length - 1) * 5000);

      return () => clearTimeout(timeout);
    }
  }, [messageIndex, messages.length]);

  const handleEmailRequest = async () => {
    const result = await queryService.requestEmailNotification(queryId);
    if (result.success) {
      setEmailRequested(true);
    } else {
      showToast(result.error || 'Failed to request email notification');
    }
  };

  return (
    <>
      <style>{`
        @keyframes fillProgress {
          from { width: 0%; }
          to { width: 90%; }
        }
        .email-link {
          color: #1976d2;
          text-decoration: underline;
          cursor: pointer;
        }
        .email-link:hover {
          color: #1565c0;
        }
      `}</style>
      <Link to="/" style={{ position: 'absolute', top: '20px', left: '20px', textDecoration: 'none', color: 'inherit', fontSize: '24px', fontWeight: 'bold' }}>
        AI Toady
      </Link>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <Link to="/">
          <img src="/toady_with_hat.png" alt="Toady with Hat" style={{ height: '200px', objectFit: 'contain', backgroundColor: 'transparent', cursor: 'pointer' }} />
        </Link>
        <div style={{ textAlign: 'center', maxWidth: '600px'}}>
          <div style={{ width: '300px', height: '8px', backgroundColor: '#555', borderRadius: '4px', margin: '0 auto 1rem', overflow: 'hidden' }}>
            <div style={{ height: '100%', backgroundColor: '#1976d2', animation: 'fillProgress 30s ease-out forwards' }} />
          </div>
          <div style={{ fontSize: '22px', marginBottom: '1rem' }}>
            {emailRequested ? (
              `Acknowledged. Check ${userEmail} for your response to "${query}".`
            ) : showEmailOption ? (
              <>
                This is taking longer than expected, click{' '}
                <span className="email-link" onClick={handleEmailRequest}>here</span>
                {' '}to get your response via email.
              </>
            ) : (
              messages[messageIndex]
            )}
          </div>
        </div>
      </div>
    </>
  );
}