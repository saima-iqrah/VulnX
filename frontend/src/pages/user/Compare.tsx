import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert, Badge, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { scansApi } from '../../api/scans';
import type { FindingKey, ScanCompare, ScanSummary } from '../../api/scans';

const label = (s: ScanSummary) =>
    `${s.label ? s.label + ' - ' : ''}${s.target} - ${new Date(s.createdAtUtc).toLocaleString()}${s.score !== null ? ` (score ${s.score})` : ''}`;

function FindingList({ title, items, empty }: { title: string; items: FindingKey[]; empty: string }) {
    return (
        <Card title={`${title} (${items.length})`}>
            {items.length === 0 && <p className="muted">{empty}</p>}
            {items.map((f, i) => (
                <div key={i} className="finding" style={{ padding: '10px 14px' }}>
                    <span className={`sev sev-${f.severity.toLowerCase()}`}>{f.severity}</span>{' '}
                    <strong>{f.title}</strong> <span className="muted">{f.code}</span>
                    <div className="muted" style={{ fontSize: 13, wordBreak: 'break-all' }}>{f.affectedUrl}</div>
                </div>
            ))}
        </Card>
    );
}

export default function Compare() {
    const [params] = useSearchParams();
    const [scans, setScans] = useState<ScanSummary[] | null>(null);
    const [baseId, setBaseId] = useState(params.get('base') ?? '');
    const [targetId, setTargetId] = useState(params.get('target') ?? '');
    const [result, setResult] = useState<ScanCompare | null>(null);
    const [error, setError] = useState('');
    const [busy, setBusy] = useState(false);
    const autoRan = useRef(false);

    const run = async (b: string, t: string) => {
        setError('');
        setResult(null);
        setBusy(true);
        try {
            setResult(await scansApi.compare(b, t));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not compare the scans.');
        } finally {
            setBusy(false);
        }
    };

    useEffect(() => {
        let on = true;
        scansApi
            .list({ status: 'Completed', pageSize: 50 })
            .then((r) => { if (on) setScans(r.items); })
            .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load scans.'); });
        return () => { on = false; };
    }, []);

    useEffect(() => {
        if (autoRan.current) return;
        const b = params.get('base');
        const t = params.get('target');
        if (b && t) {
            autoRan.current = true;
            run(b, t);
        }
    }, [params]);

    const delta = result?.scoreDelta ?? null;

    return (
        <>
            <PageHeader title="Compare scans" subtitle="See what is new, what was fixed and what stayed the same." />
            {error && <Alert kind="error">{error}</Alert>}

            <Card>
                {!scans && !error && <div className="page-center"><Spinner /></div>}
                {scans && scans.length < 2 && <p className="muted">You need at least two completed scans. <Link to="/app/scans/new">Start a scan</Link>.</p>}
                {scans && scans.length >= 2 && (
                    <div className="form">
                        <div className="field">
                            <label htmlFor="base">Older scan (baseline)</label>
                            <select id="base" className="input" value={baseId} onChange={(e) => setBaseId(e.target.value)}>
                                <option value="">Select a scan</option>
                                {scans.map((s) => <option key={s.id} value={s.id}>{label(s)}</option>)}
                            </select>
                        </div>
                        <div className="field">
                            <label htmlFor="target">Newer scan</label>
                            <select id="target" className="input" value={targetId} onChange={(e) => setTargetId(e.target.value)}>
                                <option value="">Select a scan</option>
                                {scans.map((s) => <option key={s.id} value={s.id}>{label(s)}</option>)}
                            </select>
                        </div>
                        <Button onClick={() => run(baseId, targetId)} loading={busy} disabled={!baseId || !targetId || baseId === targetId}>Compare</Button>
                    </div>
                )}
            </Card>

            {result && (
                <div style={{ marginTop: 18, display: 'grid', gap: 18 }}>
                    <div className="cmp-head">
                        <div className="card">
                            <small className="muted">Baseline</small>
                            <div className="grade">{result.base.grade ?? '-'}</div>
                            <small>Score {result.base.score ?? '-'}</small>
                        </div>
                        <div style={{ fontSize: 22, fontWeight: 700 }} className={delta === null ? '' : delta >= 0 ? 'delta-up' : 'delta-down'}>
                            {delta === null ? '' : delta > 0 ? `+${delta}` : delta}
                        </div>
                        <div className="card">
                            <small className="muted">Newer</small>
                            <div className="grade">{result.target.grade ?? '-'}</div>
                            <small>Score {result.target.score ?? '-'}</small>
                        </div>
                    </div>
                    <p className="muted">
                        <Badge tone="ok">{result.resolved.length} resolved</Badge>{' '}
                        <Badge tone="danger">{result.added.length} new</Badge>{' '}
                        <Badge>{result.unchanged.length} unchanged</Badge>
                    </p>
                    <FindingList title="New findings" items={result.added} empty="No new findings." />
                    <FindingList title="Resolved findings" items={result.resolved} empty="Nothing was resolved." />
                    <FindingList title="Unchanged findings" items={result.unchanged} empty="No findings are shared by both scans." />
                </div>
            )}
        </>
    );
}