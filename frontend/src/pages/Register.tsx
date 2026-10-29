import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Button, Field } from '../components/ui';
import { authApi } from '../api/auth';
import { homeFor, useAuth } from '../auth/AuthContext';

const strong = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/;

export default function Register() {
    const { user } = useAuth();
    const [fullName, setFullName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');
    const [error, setError] = useState('');
    const [done, setDone] = useState(false);
    const [busy, setBusy] = useState(false);

    if (user) return <Navigate to={homeFor(user)} replace />;

    const submit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        if (!strong.test(password)) {
            setError('Password needs at least 10 characters with an uppercase letter, a lowercase letter, a number and a symbol.');
            return;
        }
        if (password !== confirm) {
            setError('Passwords do not match.');
            return;
        }
        setBusy(true);
        try {
            await authApi.register(fullName.trim(), email.trim(), password);
            setDone(true);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Registration failed.');
        } finally {
            setBusy(false);
        }
    };

    if (done) {
        return (
            <AuthShell title="Account created" subtitle="Your account is ready.">
                <Alert kind="success">
                    We sent a verification link to {email}. You can sign in right away; verification may be required later.
                </Alert>
                <Link className="btn btn-primary btn-block" to="/login">Go to sign in</Link>
            </AuthShell>
        );
    }

    return (
        <AuthShell
            title="Create your account"
            subtitle="Start assessing the sites you are authorized to test."
            footer={<>Already registered? <Link to="/login">Sign in</Link></>}
        >
            {error && <Alert kind="error">{error}</Alert>}
            <form onSubmit={submit} className="form">
                <Field label="Full name" autoComplete="name" required minLength={2} value={fullName} onChange={(e) => setFullName(e.target.value)} />
                <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
                <Field
                    label="Password"
                    type="password"
                    autoComplete="new-password"
                    required
                    hint="10+ characters with upper, lower, number and symbol."
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                />
                <Field label="Confirm password" type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
                <Button type="submit" loading={busy} block>Create account</Button>
            </form>
        </AuthShell>
    );
}