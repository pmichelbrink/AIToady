import { useSearchParams, Link } from 'react-router-dom';

export default function QueryPage() {
  const [searchParams] = useSearchParams();
  const query = searchParams.get('q') || '';

  return (
    <div style={{ position: 'relative', minHeight: '100vh' }}>
      <Link to="/" style={{ position: 'absolute', top: '20px', left: '20px', textDecoration: 'none', color: 'inherit', fontSize: '24px', fontWeight: 'bold' }}>
        AI Toady
      </Link>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <Link to="/">
          <img src="/toady_with_hat.png" alt="Toady with Hat" style={{ height: '200px', objectFit: 'contain', backgroundColor: 'transparent', cursor: 'pointer' }} />
        </Link>
        <div style={{ textAlign: 'center', maxWidth: '600px'}}>
          <div style={{ fontSize: '22px', marginBottom: '1rem' }}>
            Stop right there!
          </div>
          <div style={{ fontSize: '13px' }}>
            We're still working on this. We'll know more about <strong>{query}</strong> in a little bit.
          </div>
        </div>
      </div>
    </div>
  );
}