import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Button, Field } from '../components/ui';
import { homeFor, useAuth } from '../auth/AuthContext';
import { useNoPrefill } from '../auth/useNoPrefill';

export default function Login() {
    const { user, login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const state = location.state as { from?: string; notice?: string } | null;

    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);
    const formRef = useRef<HTMLFormElement>(null);
    useNoPrefill(formRef, () => { setEmail(''); setPassword(''); });

    if (user) return <Navigate to={homeFor(user)} replace />;

    const submit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setBusy(true);
        try {
            const u = await login(email.trim(), password, 'User');
            const target = state?.from && state.from.startsWith('/app') ? state.from : homeFor(u);
            navigate(target, { replace: true });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Sign in failed.');
        } finally {
            setBusy(false);
        }
    };

    return (
        <AuthShell
            title="Welcome back"
            subtitle="Sign in to your VulnX account."
            footer={
                <>
                    New here? <Link to="/register">Create an account</Link> &middot; <Link to="/admin/login">Admin sign in</Link>
                </>
            }
        >
            {state?.notice && <Alert kind="success">{state.notice}</Alert>}
            {error && <Alert kind="error">{error}</Alert>}
            <form ref={formRef} onSubmit={submit} className="form">
                <Field label="Email" type="email" name="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
                <Field label="Password" type="password" name="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
                <Button type="submit" loading={busy} block>Sign in</Button>
            </form>
            <p className="center"><Link to="/forgot-password">Forgot your password?</Link></p>
        </AuthShell>
    );
}