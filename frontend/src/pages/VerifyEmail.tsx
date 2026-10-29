import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Spinner } from '../components/ui';
import { authApi } from '../api/auth';

export default function VerifyEmail() {
    const [params] = useSearchParams();
    const email = params.get('email') ?? '';
    const token = params.get('token') ?? '';
    const [state, setState] = useState<'working' | 'ok' | 'error'>('working');
    const [message, setMessage] = useState('');
    const started = useRef(false);   // the token is single-use, so never send it twice

    useEffect(() => {
        if (started.current) return;
        started.current = true;
        if (!email || !token) {
            setState('error');
            setMessage('This verification link is incomplete.');
            return;
        }
        authApi
            .confirmEmail(email, token)
            .then(() => setState('ok'))
            .catch((err) => {
                setState('error');
                setMessage(err instanceof Error ? err.message : 'Verification failed.');
            });
    }, [email, token]);

    return (
        <AuthShell title="Email verification" footer={<Link to="/login">Go to sign in</Link>}>
            {state === 'working' && <div className="page-center"><Spinner /></div>}
            {state === 'ok' && <Alert kind="success">Your email has been verified. You can sign in now.</Alert>}
            {state === 'error' && <Alert kind="error">{message}</Alert>}
        </AuthShell>
    );
}