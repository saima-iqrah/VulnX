import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { Alert, Badge, Card, PageHeader, Spinner, StatCard } from '../../components/ui';
import { TrendChart } from '../../components/TrendChart';
import { scansApi } from '../../api/scans';
import type { Dashboard } from '../../api/scans';

const order = ['Critical', 'High', 'Medium', 'Low', 'Informational'];

export default function UserOverview() {
    const { user } = useAuth();
    const [data, setData] = useState<Dashboard | null>(null);
    const [error, setError] = useState('');
    const firstName = user?.fullName.split(' ')[0] ?? 'there';

    useEffect(() => {
        let on = true;
        scansApi
            .dashboard()
            .then((d) => { if (on) setData(d); })
            .catch((e) => { if (on) setError(e instanceof Error ? e.message : 'Could not load the dashboard.'); });
        return () => { on = false; };
    }, []);

    const max = data ? Math.max(1, ...order.map((s) => data.severityCounts[s] ?? 0)) : 1;

    return (
        <>
            <PageHeader title={`Welcome, ${firstName}`} subtitle="Your security assessment workspace." />
            {error && <Alert kind="error">{error}</Alert>}
            {!data && !error && <div className="page-center"><Spinner /></div>}

            {data && (
                <>
                    <div className="grid4">
                        <StatCard label="Total scans" value={String(data.totalScans)} note={`${data.completedScans} completed`} />
                        <StatCard label="Latest score" value={data.latestScore === null ? '-' : `${data.latestScore} (${data.latestGrade})`} note={data.latestScore === null ? 'No completed scan yet' : 'Most recent completed scan'} />
                        <StatCard label="Open findings" value={String(data.openFindings)} note="In the latest scan" />
                        <StatCard label="Critical issues" value={String(data.criticalFindings)} />
                    </div>

                    <div className="grid2">
                        <Card title="Score trend">
                            <TrendChart points={data.trend.map((t) => ({ date: t.date, score: t.score }))} />
                        </Card>
                        <Card title="Findings by severity (latest scan)">
                            {order.map((s) => (
                                <div key={s} className="sev-bar-row">
                                    <span>{s}</span>
                                    <span className="sev-track"><span className={`sev-fill sev-${s.toLowerCase()}`} style={{ width: `${((data.severityCounts[s] ?? 0) / max) * 100}%` }} /></span>
                                    <strong>{data.severityCounts[s] ?? 0}</strong>
                                </div>
                            ))}
                        </Card>
                    </div>

                    <div style={{ marginTop: 18 }}>
                        <Card title="Recent scans">
                            {data.recent.length === 0 && (
                                <p className="muted">No scans yet. <Link to="/app/scans/new">Start your first scan</Link>.</p>
                            )}
                            {data.recent.map((s) => (
                                <Link key={s.id} to={`/app/scans/${s.id}`} className="scan-row">
                                    <span className="url">{s.label ? `${s.label} - ` : ''}{s.target}</span>
                                    <span className="scan-meta">
                                        <Badge tone={s.status === 'Completed' ? 'ok' : s.status === 'Failed' ? 'danger' : 'warn'}>{s.status}</Badge>
                                        {s.grade && <span>Grade {s.grade}</span>}
                                        <span>{new Date(s.createdAtUtc).toLocaleDateString()}</span>
                                    </span>
                                </Link>
                            ))}
                            <p style={{ marginTop: 14 }}>
                                <Link className="btn btn-primary" to="/app/scans/new">New scan</Link>
                            </p>
                        </Card>
                    </div>
                </>
            )}
        </>
    );
}