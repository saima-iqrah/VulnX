import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Button, Field } from '../components/ui';
import { authApi } from '../api/auth';

export default function ForgotPassword() {
    const [email, setEmail] = useState('');
    const [sent, setSent] = useState(false);
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);

    const submit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setBusy(true);
        try {
            await authApi.forgotPassword(email.trim());
            setSent(true);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Something went wrong.');
        } finally {
            setBusy(false);
        }
    };

    return (
        <AuthShell
            title="Reset your password"
            subtitle="Enter your email and we will send a reset link."
            footer={<Link to="/login">Back to sign in</Link>}
        >
            {sent && <Alert kind="success">If an account with that email exists, a reset link has been sent.</Alert>}
            {error && <Alert kind="error">{error}</Alert>}
            {!sent && (
                <form onSubmit={submit} className="form">
                    <Field label="Email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
                    <Button type="submit" loading={busy} block>Send reset link</Button>
                </form>
            )}
        </AuthShell>
    );
}