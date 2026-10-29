import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Button, Field } from '../components/ui';
import { authApi } from '../api/auth';

const strong = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/;

export default function ResetPassword() {
    const [params] = useSearchParams();
    const navigate = useNavigate();
    const email = params.get('email') ?? '';
    const token = params.get('token') ?? '';

    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);

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
            await authApi.resetPassword(email, token, password);
            navigate('/login', { replace: true, state: { notice: 'Password updated. Please sign in.' } });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Could not reset the password.');
        } finally {
            setBusy(false);
        }
    };

    if (!email || !token) {
        return (
            <AuthShell title="Invalid link" footer={<Link to="/forgot-password">Request a new link</Link>}>
                <Alert kind="error">This reset link is incomplete or invalid.</Alert>
            </AuthShell>
        );
    }

    return (
        <AuthShell title="Choose a new password" footer={<Link to="/login">Back to sign in</Link>}>
            {error && <Alert kind="error">{error}</Alert>}
            <form onSubmit={submit} className="form">
                <Field label="New password" type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
                <Field label="Confirm new password" type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
                <Button type="submit" loading={busy} block>Update password</Button>
            </form>
        </AuthShell>
    );
}