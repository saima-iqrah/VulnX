import { useEffect, useState } from 'react';
import { Alert, Badge, Card, PageHeader, Spinner, StatCard } from '../../components/ui';
import { adminApi } from '../../api/platform';
import type { AdminOverview as Overview, SystemHealth } from '../../api/platform';

const uptime = (s: number) => {
    const d = Math.floor(s / 86400);
    const h = Math.floor((s % 86400) / 3600);
    const m = Math.floor((s % 3600) / 60);
    return `${d}d ${h}h ${m}m`;
};

export default function AdminOverview() {
    const [data, setData] = useState<Overview | null>(null);
    const [health, setHealth] = useState<SystemHealth | null>(null);
    const [error, setError] = useState('');

    useEffect(() => {
        let on = true;
        const load = () => {
            adminApi.overview().then((d) => { if (on) setData(d); }).catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load the overview.'); });
            adminApi.health().then((h) => { if (on) setHealth(h); }).catch(() => undefined);
        };
        load();
        const timer = setInterval(load, 30000);
        return () => { on = false; clearInterval(timer); };
    }, []);

    const max = data ? Math.max(1, ...data.scansPerDay.map((d) => d.count)) : 1;

    return (
        <>
            <PageHeader title="Admin overview" subtitle="Platform activity and system health." />
            {error && <Alert kind="error">{error}</Alert>}
            {!data && !error && <div className="page-center"><Spinner /></div>}

            {data && (
                <>
                    <div className="grid4">
                        <StatCard label="Users" value={String(data.users)} note={`${data.newUsers7d} new in 7 days, ${data.admins} admin(s)`} />
                        <StatCard label="Suspended or locked" value={String(data.suspendedUsers)} />
                        <StatCard label="Scans" value={String(data.scans)} note={`${data.scans24h} in 24 h, ${data.runningScans} running`} />
                        <StatCard label="Critical findings" value={String(data.criticalFindings)} note={`${data.failedScans24h} failed scans in 24 h`} />
                    </div>

                    <div className="grid2">
                        <Card title="Scans per day (last 7 days)">
                            {data.scansPerDay.map((d) => (
                                <div key={d.date} className="sev-bar-row">
                                    <span>{new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</span>
                                    <span className="sev-track"><span className="sev-fill sev-low" style={{ width: `${(d.count / max) * 100}%` }} /></span>
                                    <strong>{d.count}</strong>
                                </div>
                            ))}
                        </Card>

                        <Card title="System health">
                            {!health && <Spinner />}
                            {health && (
                                <ul className="health">
                                    <li><span>Status</span><Badge tone={health.databaseOk ? 'ok' : 'danger'}>{health.status}</Badge></li>
                                    <li><span>Database latency</span><Badge>{health.databaseLatencyMs} ms</Badge></li>
                                    <li><span>Scan queue</span><Badge>{health.queuedScans} queued, {health.runningScans} running</Badge></li>
                                    <li><span>Last completed scan</span><Badge>{health.lastCompletedScanUtc ? new Date(health.lastCompletedScanUtc).toLocaleString() : '-'}</Badge></li>
                                    <li><span>AI assistant</span><Badge tone={health.aiConfigured ? 'ok' : 'default'}>{health.aiConfigured ? 'Connected' : 'Built-in only'}</Badge></li>
                                    <li><span>Email</span><Badge tone={health.emailConfigured ? 'ok' : 'default'}>{health.emailConfigured ? 'SMTP' : 'Log only'}</Badge></li>
                                    <li><span>Environment</span><Badge>{health.environment}</Badge></li>
                                    <li><span>Uptime / memory</span><Badge>{uptime(health.uptimeSeconds)} / {health.memoryMb} MB</Badge></li>
                                </ul>
                            )}
                        </Card>
                    </div>
                </>
            )}
        </>
    );
}