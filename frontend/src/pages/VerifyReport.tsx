import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';
import { Alert, Button, Field } from '../components/ui';
import { reportsApi } from '../api/platform';
import type { VerifyResult } from '../api/platform';

export default function VerifyReport() {
    const [params] = useSearchParams();
    const [number, setNumber] = useState(params.get('number') ?? '');
    const [code, setCode] = useState(params.get('code') ?? '');
    const [result, setResult] = useState<VerifyResult | null>(null);
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);
    const auto = useRef(false);

    const verify = async (n: string, c: string) => {
        setError('');
        setResult(null);
        setBusy(true);
        try {
            setResult(await reportsApi.verify(n.trim(), c.trim()));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Verification failed.');
        } finally {
            setBusy(false);
        }
    };

    useEffect(() => {
        if (auto.current) return;
        const n = params.get('number');
        const c = params.get('code');
        if (n && c) {
            auto.current = true;
            verify(n, c);
        }
    }, [params]);

    const submit = (e: FormEvent) => {
        e.preventDefault();
        verify(number, code);
    };

    return (
        <AuthShell
            title="Verify a report"
            subtitle="Check that a VulnX PDF report is genuine and unchanged."
            footer={<Link to="/">Back to home</Link>}
        >
            {error && <Alert kind="error">{error}</Alert>}
            <form onSubmit={submit} className="form">
                <Field label="Report number" placeholder="VX-2026-1A2B3C4D" required value={number} onChange={(e) => setNumber(e.target.value)} />
                <Field label="Verification code" placeholder="ABCD-EF01-2345-6789" required value={code} onChange={(e) => setCode(e.target.value)} />
                <Button type="submit" loading={busy}>Verify</Button>
            </form>

            {result && (
                <div className={result.valid ? 'verify-ok' : 'verify-bad'}>
                    <strong>{result.valid ? 'Valid report' : 'Not verified'}</strong>
                    <p style={{ margin: '6px 0 0' }}>{result.message}</p>
                    {result.valid && (
                        <ul style={{ margin: '10px 0 0', paddingLeft: 18 }}>
                            <li>Report: {result.reportNumber}</li>
                            <li>Site: {result.host}</li>
                            <li>Generated: {result.generatedAtUtc ? new Date(result.generatedAtUtc).toLocaleString() : '-'}</li>
                            <li>Score: {result.score} (grade {result.grade})</li>
                            <li>Findings: {result.findings} ({result.critical} critical, {result.high} high)</li>
                        </ul>
                    )}
                </div>
            )}
        </AuthShell>
    );
}