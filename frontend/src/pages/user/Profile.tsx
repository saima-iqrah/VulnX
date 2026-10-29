import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Card, Field, PageHeader } from '../../components/ui';
import { authApi } from '../../api/auth';
import { setAccessToken } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';

const strong = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/;

export default function Profile() {
    const { user, setUser, logout } = useAuth();
    const navigate = useNavigate();

    const [fullName, setFullName] = useState(user?.fullName ?? '');
    const [profileMsg, setProfileMsg] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);
    const [profileBusy, setProfileBusy] = useState(false);

    const [current, setCurrent] = useState('');
    const [next, setNext] = useState('');
    const [confirm, setConfirm] = useState('');
    const [pwError, setPwError] = useState('');
    const [pwBusy, setPwBusy] = useState(false);

    const saveProfile = async (e: FormEvent) => {
        e.preventDefault();
        setProfileMsg(null);
        setProfileBusy(true);
        try {
            const updated = await authApi.updateProfile(fullName.trim());
            setUser(updated);
            setProfileMsg({ kind: 'success', text: 'Profile updated.' });
        } catch (err) {
            setProfileMsg({ kind: 'error', text: err instanceof Error ? err.message : 'Update failed.' });
        } finally {
            setProfileBusy(false);
        }
    };

    const changePassword = async (e: FormEvent) => {
        e.preventDefault();
        setPwError('');
        if (!strong.test(next)) {
            setPwError('New password needs at least 10 characters with an uppercase letter, a lowercase letter, a number and a symbol.');
            return;
        }
        if (next !== confirm) {
            setPwError('New passwords do not match.');
            return;
        }
        setPwBusy(true);
        try {
            await authApi.changePassword(current, next);
            // The server revoked every session, so sign in again.
            setAccessToken(null);
            await logout();
            navigate('/login', { replace: true, state: { notice: 'Password changed. Please sign in again.' } });
        } catch (err) {
            setPwError(err instanceof Error ? err.message : 'Could not change the password.');
        } finally {
            setPwBusy(false);
        }
    };

    return (
        <>
            <PageHeader title="Profile" subtitle="Manage your account details and password." />
            <div className="grid2">
                <Card title="Details">
                    {profileMsg && <Alert kind={profileMsg.kind}>{profileMsg.text}</Alert>}
                    <form onSubmit={saveProfile} className="form">
                        <Field label="Full name" required minLength={2} value={fullName} onChange={(e) => setFullName(e.target.value)} />
                        <Field label="Email" value={user?.email ?? ''} disabled readOnly />
                        <Button type="submit" loading={profileBusy}>Save changes</Button>
                    </form>
                </Card>

                <Card title="Change password">
                    {pwError && <Alert kind="error">{pwError}</Alert>}
                    <form onSubmit={changePassword} className="form">
                        <Field label="Current password" type="password" autoComplete="current-password" required value={current} onChange={(e) => setCurrent(e.target.value)} />
                        <Field label="New password" type="password" autoComplete="new-password" required value={next} onChange={(e) => setNext(e.target.value)} />
                        <Field label="Confirm new password" type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
                        <Button type="submit" loading={pwBusy}>Change password</Button>
                    </form>
                </Card>
            </div>
        </>
    );
}
