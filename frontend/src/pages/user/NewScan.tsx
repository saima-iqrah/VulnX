import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Button, Card, Field, PageHeader } from '../../components/ui';
import { scansApi } from '../../api/scans';

const profiles = [
    { value: 'Quick', label: 'Quick', desc: 'Security headers, TLS and cookies. About 30 seconds.' },
    { value: 'Standard', label: 'Standard', desc: 'Adds CORS, information disclosure and HTML content checks.' },
    { value: 'Deep', label: 'Deep', desc: 'Same checks with a larger request budget and longer time limit.' }
];

export default function NewScan() {
    const navigate = useNavigate();
    const [params] = useSearchParams();
  const [url, setUrl] = useState(params.get('url') ?? '');
    const [profile, setProfile] = useState(['Quick', 'Standard', 'Deep'].includes(params.get('profile') ?? '') ? (params.get('profile') as string) : 'Quick');
    const [confirmed, setConfirmed] = useState(false);
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);

    const submit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setBusy(true);
        try {
            const scan = await scansApi.create(url.trim(), profile, confirmed);
            navigate(`/app/scans/${scan.id}`);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Could not start the scan.');
        } finally {
            setBusy(false);
        }
    };

    return (
        <>
            <PageHeader title="New scan" subtitle="Scan a website you own or are authorized to test." />
            <Card>
                {error && <Alert kind="error">{error}</Alert>}
                <form onSubmit={submit} className="form">
                    <Field
                        label="Target URL"
                        placeholder="https://example.com"
                        required
                        value={url}
                        onChange={(e) => setUrl(e.target.value)}
                        hint="Public http/https sites only. Localhost, private networks and internal hosts are blocked."
                    />
                    <div className="field">
                        <label htmlFor="profile">Scan profile</label>
                        <select id="profile" className="input" value={profile} onChange={(e) => setProfile(e.target.value)}>
                            {profiles.map((p) => (
                                <option key={p.value} value={p.value}>{p.label}</option>
                            ))}
                        </select>
                        <small>{profiles.find((p) => p.value === profile)?.desc}</small>
                    </div>
                    <label className="check">
                        <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
                        <span>I confirm that I own this website or have explicit written permission to test it.</span>
                    </label>
                    <Button type="submit" loading={busy} disabled={!confirmed}>Start scan</Button>
                </form>
            </Card>
        </>
    );
}


