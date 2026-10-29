import { useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, PageHeader, Spinner } from '../../components/ui';
import { notificationsApi } from '../../api/platform';
import type { NotificationItem } from '../../api/platform';

const tone = (t: string): 'ok' | 'warn' | 'danger' | 'default' =>
    t === 'CriticalFinding' ? 'danger' : t === 'ScanCompleted' ? 'ok' : t === 'Announcement' ? 'warn' : 'default';

export default function Notifications() {
    const [items, setItems] = useState<NotificationItem[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [unreadOnly, setUnreadOnly] = useState(false);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);

    useEffect(() => {
        let on = true;
        notificationsApi
            .list(page, unreadOnly)
            .then((r) => { if (on) { setItems(r.items); setTotal(r.total); setError(''); } })
            .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load notifications.'); });
        return () => { on = false; };
    }, [page, unreadOnly, reload]);

    const run = async (fn: () => Promise<unknown>) => {
        try { await fn(); setReload((r) => r + 1); }
        catch (e) { setError(e instanceof Error ? e.message : 'Action failed.'); }
    };

    const pages = Math.max(1, Math.ceil(total / 20));

    return (
        <>
            <PageHeader title="Notifications" subtitle="Scan results and announcements." />
            {error && <Alert kind="error">{error}</Alert>}
            <Card>
                <div className="toolbar">
                    <label className="check">
                        <input type="checkbox" checked={unreadOnly} onChange={(e) => { setUnreadOnly(e.target.checked); setPage(1); }} />
                        <span>Unread only</span>
                    </label>
                    <Button variant="ghost" onClick={() => run(() => notificationsApi.markAllRead())}>Mark all as read</Button>
                </div>

                {!items && !error && <div className="page-center"><Spinner /></div>}
                {items && items.length === 0 && <p className="muted">No notifications.</p>}

                {items?.map((n) => (
                    <div key={n.id} className={`notif ${n.isRead ? '' : 'unread'}`}>
                        <div>
                            <Badge tone={tone(n.type)}>{n.type}</Badge> <strong>{n.title}</strong>
                            <p>{n.message}</p>
                            <small className="muted">{new Date(n.createdAtUtc).toLocaleString()}</small>
                        </div>
                        <div className="actions">
                            {!n.isRead && <Button variant="ghost" onClick={() => run(() => notificationsApi.markRead(n.id))}>Mark read</Button>}
                            <Button variant="ghost" onClick={() => run(() => notificationsApi.remove(n.id))}>Delete</Button>
                        </div>
                    </div>
                ))}

                {total > 20 && (
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