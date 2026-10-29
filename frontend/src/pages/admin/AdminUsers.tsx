import { useEffect, useState } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { Alert, Badge, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { adminApi } from '../../api/platform';
import type { AdminUser } from '../../api/platform';

const PAGE = 20;
const tone = (s: string): 'ok' | 'warn' | 'danger' | 'default' => (s === 'Active' ? 'ok' : s === 'Suspended' ? 'warn' : 'danger');

export default function AdminUsers() {
    const { user: me } = useAuth();
    const [items, setItems] = useState<AdminUser[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [role, setRole] = useState('');
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);

    useEffect(() => {
        let on = true;
        const t = setTimeout(() => {
            adminApi
                .users({ page, pageSize: PAGE, search, status, role })
                .then((r) => { if (on) { setItems(r.items); setTotal(r.total); setError(''); } })
                .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load users.'); });
        }, 250);
        return () => { on = false; clearTimeout(t); };
    }, [page, search, status, role, reload]);

    const run = async (fn: () => Promise<unknown>) => {
        setError('');
        try { await fn(); setReload((r) => r + 1); }
        catch (e) { setError(e instanceof Error ? e.message : 'Action failed.'); }
    };

    const changeStatus = (u: AdminUser, next: string) => {
        if (next === u.status) return;
        if (!window.confirm(`Change ${u.email} to ${next}?`)) return;
        run(() => adminApi.setStatus(u.id, next));
    };

    const pages = Math.max(1, Math.ceil(total / PAGE));

    return (
        <>
            <PageHeader title="Users" subtitle="Search accounts, suspend or lock them, change roles." />
            {error && <Alert kind="error">{error}</Alert>}
            <Card>
                <div className="toolbar">
                    <input className="input" placeholder="Search name or email..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
                    <select className="input" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} aria-label="Status">
                        <option value="">All statuses</option>
                        <option value="Active">Active</option>
                        <option value="Suspended">Suspended</option>
                        <option value="Locked">Locked</option>
                    </select>
                    <select className="input" value={role} onChange={(e) => { setRole(e.target.value); setPage(1); }} aria-label="Role">
                        <option value="">All roles</option>
                        <option value="User">User</option>
                        <option value="Admin">Admin</option>
                    </select>
                </div>

                {!items && !error && <div className="page-center"><Spinner /></div>}
                {items && (
                    <div className="table-wrap">
                        <table className="table">
                            <thead>
                                <tr><th>User</th><th>Role</th><th>Status</th><th>Scans</th><th>Joined</th><th>Last login</th><th>Actions</th></tr>
                            </thead>
                            <tbody>
                                {items.map((u) => {
                                    const self = u.id === me?.id;
                                    return (
                                        <tr key={u.id}>
                                            <td><strong>{u.fullName}</strong><div className="muted">{u.email}</div></td>
                                            <td>{u.isAdmin ? <Badge tone="warn">Admin</Badge> : <Badge>User</Badge>}</td>
                                            <td>
                                                {self ? <Badge tone={tone(u.status)}>{u.status}</Badge> : (
                                                    <select className="input" value={u.status} onChange={(e) => changeStatus(u, e.target.value)} aria-label="Change status">
                                                        <option>Active</option>
                                                        <option>Suspended</option>
                                                        <option>Locked</option>
                                                    </select>
                                                )}
                                            </td>
                                            <td>{u.scanCount}</td>
                                            <td>{new Date(u.createdAtUtc).toLocaleDateString()}</td>
                                            <td>{u.lastLoginAtUtc ? new Date(u.lastLoginAtUtc).toLocaleString() : '-'}</td>
                                            <td>
                                                {self ? <span className="muted">You</span> : (
                                                    <div className="actions">
                                                        <Button variant="ghost" onClick={() => window.confirm(`Make ${u.email} ${u.isAdmin ? 'a regular user' : 'an admin'}? They must sign in again.`) && run(() => adminApi.setRole(u.id, !u.isAdmin))}>
                                                            {u.isAdmin ? 'Make user' : 'Make admin'}
                                                        </Button>
                                                        <Button variant="danger" onClick={() => window.confirm(`Delete ${u.email} and ALL of their scans? This cannot be undone.`) && run(() => adminApi.deleteUser(u.id))}>
                                                            Delete
                                                        </Button>
                                                    </div>
                                                )}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                        {items.length === 0 && <p className="muted">No users found.</p>}
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