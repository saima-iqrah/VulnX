import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, Badge, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { scansApi } from '../../api/scans';
import type { ScanSummary } from '../../api/scans';

const tone = (s: string): 'ok' | 'warn' | 'danger' | 'default' =>
    s === 'Completed' ? 'ok' : s === 'Running' ? 'warn' : s === 'Failed' ? 'danger' : 'default';

const PAGE_SIZE = 20;

export default function ScanHistory() {
    const navigate = useNavigate();
    const [items, setItems] = useState<ScanSummary[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [tag, setTag] = useState('');
    const [selected, setSelected] = useState<ScanSummary[]>([]);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);

    useEffect(() => {
        let on = true;
        const timer = setTimeout(() => {
            scansApi
                .list({ page, pageSize: PAGE_SIZE, search, status, tag })
                .then((r) => { if (on) { setItems(r.items); setTotal(r.total); setError(''); } })
                .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load scans.'); });
        }, 250);
        return () => { on = false; clearTimeout(timer); };
    }, [page, search, status, tag, reload]);

    const toggle = (s: ScanSummary) =>
        setSelected((cur) => (cur.some((c) => c.id === s.id) ? cur.filter((c) => c.id !== s.id) : [...cur.slice(-1), s]));

    const compare = () => {
        if (selected.length !== 2) return;
        if (selected.some((s) => s.status !== 'Completed')) {
            setError('Only completed scans can be compared.');
            return;
        }
        const [older, newer] = [...selected].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
        navigate(`/app/compare?base=${older.id}&target=${newer.id}`);
    };

    const remove = async (s: ScanSummary) => {
        if (!window.confirm(`Delete the scan of ${s.target}?`)) return;
        try {
            await scansApi.remove(s.id);
            setSelected((cur) => cur.filter((c) => c.id !== s.id));
            setReload((r) => r + 1);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not delete the scan.');
        }
    };

    const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

    return (
        <>
            <PageHeader title="Scan history" subtitle="Search, filter, compare and manage your scans." />
            {error && <Alert kind="error">{error}</Alert>}

            <Card>
                <div className="toolbar">
                    <input className="input" placeholder="Search URL or label..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
                    <select className="input" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} aria-label="Status filter">
                        <option value="">All statuses</option>
                        <option value="Completed">Completed</option>
                        <option value="Running">Running</option>
                        <option value="Queued">Queued</option>
                        <option value="Failed">Failed</option>
                        <option value="Cancelled">Cancelled</option>
                    </select>
                    {tag && <button className="tag" onClick={() => { setTag(''); setPage(1); }}>Tag: {tag} &times;</button>}
                    <Button variant="ghost" onClick={compare} disabled={selected.length !== 2}>Compare selected ({selected.length}/2)</Button>
                </div>

                {!items && !error && <div className="page-center"><Spinner /></div>}
                {items && items.length === 0 && (
                    <p className="muted">No scans found. <Link to="/app/scans/new">Start a scan</Link>.</p>
                )}

                {items?.map((s) => (
                    <div key={s.id} className="row-check">
                        <input type="checkbox" aria-label="Select for comparison" checked={selected.some((c) => c.id === s.id)} onChange={() => toggle(s)} />
                        <Link to={`/app/scans/${s.id}`} className="scan-row">
                            <span className="url">{s.label ? `${s.label} - ` : ''}{s.target}</span>
                            <span className="scan-meta">
                                <Badge tone={tone(s.status)}>{s.status}</Badge>
                                {s.grade && <span>Grade {s.grade} ({s.score})</span>}
                                <span>{s.findingsCount} findings</span>
                                <span>{new Date(s.createdAtUtc).toLocaleString()}</span>
                            </span>
                        </Link>
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                            {s.tags.map((t) => (
                                <button key={t} className="tag" onClick={() => { setTag(t); setPage(1); }}>{t}</button>
                            ))}
                            {(s.status === 'Completed' || s.status === 'Failed' || s.status === 'Cancelled') && (
                                <Button variant="ghost" onClick={() => remove(s)}>Delete</Button>
                            )}
                        </div>
                    </div>
                ))}

                {total > PAGE_SIZE && (
                    <div className="pager">
                        <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
                        <span className="muted">Page {page} of {pages}</span>
                        <Button variant="ghost" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Next</Button>
                    </div>
                )}
            </Card>
        </>
    );
}