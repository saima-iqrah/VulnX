import { useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { adminApi } from '../../api/platform';
import type { AdminScan } from '../../api/platform';

const PAGE = 20;
const tone = (s: string): 'ok' | 'warn' | 'danger' | 'default' =>
    s === 'Completed' ? 'ok' : s === 'Running' ? 'warn' : s === 'Failed' ? 'danger' : 'default';

export default function AdminScans() {
    const [items, setItems] = useState<AdminScan[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);

    useEffect(() => {
        let on = true;
        const t = setTimeout(() => {
            adminApi
                .scans({ page, pageSize: PAGE, search, status })
                .then((r) => { if (on) { setItems(r.items); setTotal(r.total); setError(''); } })
                .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load scans.'); });
        }, 250);
        return () => { on = false; clearTimeout(t); };
    }, [page, search, status, reload]);

    const remove = async (s: AdminScan) => {
        if (!window.confirm(`Delete the scan of ${s.target} (${s.ownerEmail})?`)) return;
        try { await adminApi.deleteScan(s.id); setReload((r) => r + 1); }
        catch (e) { setError(e instanceof Error ? e.message : 'Could not delete the scan.'); }
    };

    const pages = Math.max(1, Math.ceil(total / PAGE));

    return (
        <>
            <PageHeader title="All scans" subtitle="Every scan on the platform." />
            {error && <Alert kind="error">{error}</Alert>}
            <Card>
                <div className="toolbar">
                    <input className="input" placeholder="Search URL, owner or label..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
                    <select className="input" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} aria-label="Status">
                        <option value="">All statuses</option>
                        <option>Completed</option>
                        <option>Running</option>
                        <option>Queued</option>
                        <option>Failed</option>
                        <option>Cancelled</option>
                    </select>
                </div>

                {!items && !error && <div className="page-center"><Spinner /></div>}
                {items && (
                    <div className="table-wrap">
                        <table className="table">
                            <thead>
                                <tr><th>Target</th><th>Owner</th><th>Status</th><th>Grade</th><th>Findings</th><th>Created</th><th></th></tr>
                            </thead>
                            <tbody>
                                {items.map((s) => (
                                    <tr key={s.id}>
                                        <td className="mono">{s.label ? `${s.label} - ` : ''}{s.target}</td>
                                        <td>{s.ownerEmail}</td>
                                        <td><Badge tone={tone(s.status)}>{s.status}</Badge></td>
                                        <td>{s.grade ? `${s.grade} (${s.score})` : '-'}</td>
                                        <td>{s.findingsCount}</td>
                                        <td>{new Date(s.createdAtUtc).toLocaleString()}</td>
                                        <td><Button variant="danger" onClick={() => remove(s)}>Delete</Button></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                        {items.length === 0 && <p className="muted">No scans found.</p>}
                    </div>
                )}

                {total > PAGE && (
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