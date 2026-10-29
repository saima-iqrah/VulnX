import { useEffect, useState } from 'react';
import { Alert, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { adminApi } from '../../api/platform';
import type { AuditEntry } from '../../api/platform';

const PAGE = 30;

export default function AdminAudit() {
    const [items, setItems] = useState<AuditEntry[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState('');
    const [error, setError] = useState('');

    useEffect(() => {
        let on = true;
        const t = setTimeout(() => {
            adminApi
                .audit({ page, pageSize: PAGE, search })
                .then((r) => { if (on) { setItems(r.items); setTotal(r.total); setError(''); } })
                .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load the audit log.'); });
        }, 250);
        return () => { on = false; clearTimeout(t); };
    }, [page, search]);

    const pages = Math.max(1, Math.ceil(total / PAGE));

    return (
        <>
            <PageHeader title="Audit logs" subtitle="Security-relevant actions. Secrets are never stored here." />
            {error && <Alert kind="error">{error}</Alert>}
            <Card>
                <div className="toolbar">
                    <input className="input" placeholder="Search action, entity or user email..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
                </div>

                {!items && !error && <div className="page-center"><Spinner /></div>}
                {items && (
                    <div className="table-wrap">
                        <table className="table">
                            <thead>
                                <tr><th>Time</th><th>User</th><th>Action</th><th>Entity</th><th>Details</th></tr>
                            </thead>
                            <tbody>
                                {items.map((a) => (
                                    <tr key={a.id}>
                                        <td>{new Date(a.createdAtUtc).toLocaleString()}</td>
                                        <td>{a.userEmail ?? <span className="muted">{a.userId ? 'deleted user' : 'system'}</span>}</td>
                                        <td><strong>{a.action}</strong></td>
                                        <td className="mono">{a.entityType}{a.entityId ? ` ${a.entityId.slice(0, 8)}` : ''}</td>
                                        <td className="mono">{a.metadata ?? ''}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                        {items.length === 0 && <p className="muted">No events found.</p>}
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