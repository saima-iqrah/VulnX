import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Badge, Button, Field } from '../components/ui';
import { homeFor, useAuth } from '../auth/AuthContext';
import { useNoPrefill } from '../auth/useNoPrefill';

export default function AdminLogin() {
    const { user, login } = useAuth();
    const navigate = useNavigate();

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
            await login(email.trim(), password, 'Admin');
            navigate('/admin', { replace: true });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Sign in failed.');
        } finally {
            setBusy(false);
        }
    };

    return (
        <AuthShell
            title="Admin sign in"
            subtitle="Restricted to platform administrators."
            footer={<Link to="/login">Regular user? Sign in here</Link>}
        >
            <Badge tone="warn">Restricted area</Badge>
            {error && <Alert kind="error">{error}</Alert>}
            <form ref={formRef} onSubmit={submit} className="form">
                <Field label="Admin email" type="email" name="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
                <Field label="Password" type="password" name="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
                <Button type="submit" loading={busy} block>Sign in as admin</Button>
            </form>
            <p className="center"><Link to="/forgot-password">Forgot your password?</Link></p>
        </AuthShell>
    );
}