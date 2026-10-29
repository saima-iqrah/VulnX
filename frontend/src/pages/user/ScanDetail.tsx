import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import * as signalR from '@microsoft/signalr';
import { Alert, Badge, Button, Card, Field, PageHeader, Spinner } from '../../components/ui';
import { AiChat } from '../../components/AiChat';
import { getAccessToken } from '../../api/client';
import { isActive, scansApi } from '../../api/scans';
import type { ScanDetail as ScanDetailData, ScanProgress } from '../../api/scans';
import { aiApi } from '../../api/ai';
import { downloadExport } from '../../api/extras';
import { reportsApi } from '../../api/platform';
import type { AiExplanation, AiSummary } from '../../api/ai';

const statusTone = (s: string): 'ok' | 'warn' | 'danger' | 'default' =>
    s === 'Completed' ? 'ok' : s === 'Running' ? 'warn' : s === 'Failed' ? 'danger' : 'default';

const severityOrder = ['Critical', 'High', 'Medium', 'Low', 'Informational'];

export default function ScanDetail() {
    const { id } = useParams<{ id: string }>();
    const navigate = useNavigate();
    const [scan, setScan] = useState<ScanDetailData | null>(null);
    const [error, setError] = useState('');
    const [cancelling, setCancelling] = useState(false);
    const scanRef = useRef<ScanDetailData | null>(null);
    scanRef.current = scan;

    // label / notes / tags editor
    const [form, setForm] = useState({ label: '', notes: '', tags: '' });
    const [saveMsg, setSaveMsg] = useState('');
    const [saving, setSaving] = useState(false);
    const initialized = useRef(false);

    // AI
    const [explanations, setExplanations] = useState<Record<string, AiExplanation>>({});
    const [explaining, setExplaining] = useState<string | null>(null);
    const [summary, setSummary] = useState<AiSummary | null>(null);
    const [summarizing, setSummarizing] = useState(false);

    useEffect(() => {
        if (!id) return;
        let stopped = false;

        const load = () =>
            scansApi
                .get(id)
                .then((d) => { if (!stopped) setScan(d); })
                .catch((e) => { if (!stopped) setError(e instanceof Error ? e.message : 'Could not load the scan.'); });

        load();

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/scans', { accessTokenFactory: () => getAccessToken() ?? '' })
            .withAutomaticReconnect()
            .build();

        connection.on('scanProgress', (p: ScanProgress) => {
            if (p.scanId !== id) return;
            setScan((prev) =>
                prev ? { ...prev, status: p.status, progressPercent: p.progressPercent, modules: p.modules, score: p.score, grade: p.grade, error: p.error } : prev
            );
            if (!isActive(p.status)) load();
        });

        connection.start().then(() => connection.invoke('JoinScan', id)).catch(() => { /* polling below is the fallback */ });

        const poll = setInterval(() => {
            if (scanRef.current && isActive(scanRef.current.status)) load();
        }, 5000);

        return () => {
            stopped = true;
            clearInterval(poll);
            connection.stop();
        };
    }, [id]);

    useEffect(() => {
        if (scan && !initialized.current) {
            initialized.current = true;
            setForm({ label: scan.label ?? '', notes: scan.notes ?? '', tags: scan.tags.join(', ') });
        }
    }, [scan]);

    const cancel = async () => {
        if (!id) return;
        setCancelling(true);
        try {
            await scansApi.cancel(id);
            setScan(await scansApi.get(id));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not cancel the scan.');
        } finally {
            setCancelling(false);
        }
    };

    const save = async (e: FormEvent) => {
        e.preventDefault();
        if (!id) return;
        setSaving(true);
        setSaveMsg('');
        try {
            const updated = await scansApi.update(id, {
                label: form.label,
                notes: form.notes,
                tags: form.tags.split(',').map((t) => t.trim()).filter(Boolean)
            });
            setScan(updated);
            setSaveMsg('Saved.');
        } catch (err) {
            setSaveMsg(err instanceof Error ? err.message : 'Could not save.');
        } finally {
            setSaving(false);
        }
    };

    const [reporting, setReporting] = useState(false);

  const downloadReport = async () => {
    if (!id) return;
    setReporting(true);
    setError('');
    try {
      await reportsApi.download(id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not create the report.');
    } finally {
      setReporting(false);
    }
  };

  const remove = async () => {
        if (!id || !window.confirm('Delete this scan and all of its findings? This cannot be undone.')) return;
        try {
            await scansApi.remove(id);
            navigate('/app/history', { replace: true });
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not delete the scan.');
        }
    };

    const explain = async (resultId: string) => {
        if (!id) return;
        setExplaining(resultId);
        try {
            const ex = await aiApi.explain(id, resultId);
            setExplanations((m) => ({ ...m, [resultId]: ex }));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not explain the finding.');
        } finally {
            setExplaining(null);
        }
    };

    const summarize = async () => {
        if (!id) return;
        setSummarizing(true);
        try {
            setSummary(await aiApi.summary(id));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not create the summary.');
        } finally {
            setSummarizing(false);
        }
    };

    if (error && !scan) return <Alert kind="error">{error}</Alert>;
    if (!scan) return <div className="page-center"><Spinner /></div>;

    const active = isActive(scan.status);
    const counts = severityOrder
        .map((s) => ({ s, n: scan.results.filter((r) => r.severity === s).length }))
        .filter((c) => c.n > 0);

    return (
        <>
            <p><Link to="/app/history">&larr; Scan history</Link></p>
            <PageHeader title={scan.label || scan.target} subtitle={`${scan.target} - ${scan.profile} scan`} />
            {error && <Alert kind="error">{error}</Alert>}

            <Card>
                <div className="scan-head">
                    <div>
                        <Badge tone={statusTone(scan.status)}>{scan.status}</Badge>
                        {scan.error && <p className="muted" style={{ marginTop: 10 }}>{scan.error}</p>}
                    </div>
                    {scan.grade && (
                        <div style={{ textAlign: 'center' }}>
                            <div className="grade">{scan.grade}</div>
                            <small className="muted">Score {scan.score}/100</small>
                        </div>
                    )}
                    {active && <Button variant="ghost" onClick={cancel} loading={cancelling}>Cancel scan</Button>}
          {!active && <Link className="btn btn-ghost" to={`/app/scans/new?url=${encodeURIComponent(scan.target)}&profile=${scan.profile}`}>Scan again</Link>}
          {scan.status === 'Completed' && (
            <>
              <Button variant="ghost" onClick={() => downloadExport(scan.id, 'csv').catch((e) => setError(e instanceof Error ? e.message : 'Export failed.'))}>Export CSV</Button>
              <Button variant="ghost" onClick={() => downloadExport(scan.id, 'json').catch((e) => setError(e instanceof Error ? e.message : 'Export failed.'))}>Export JSON</Button>
            </>
          )}
          {scan.status === 'Completed' && <Button variant="ghost" onClick={downloadReport} loading={reporting}>Download PDF report</Button>}
                </div>

                <div style={{ marginTop: 18 }}>
                    <div className="progress" aria-label="Scan progress"><span style={{ width: `${scan.progressPercent}%` }} /></div>
                    <small className="muted">{scan.progressPercent}%</small>
                </div>

                <ul className="mod-list">
                    {scan.modules.map((m) => (
                        <li key={m.name}>
                            <span>{m.name}{m.error ? <small className="muted"> - {m.error}</small> : null}</span>
                            <Badge tone={statusTone(m.status)}>{m.status}</Badge>
                        </li>
                    ))}
                </ul>
            </Card>

            <div style={{ marginTop: 18 }}>
                <Card title="Label, notes and tags">
                    <form onSubmit={save} className="form">
                        <Field label="Label" maxLength={120} value={form.label} onChange={(e) => setForm({ ...form, label: e.target.value })} />
                        <div className="field">
                            <label htmlFor="notes">Notes</label>
                            <textarea id="notes" className="input" maxLength={2000} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
                        </div>
                        <Field label="Tags" hint="Separate with commas, up to 10 (for example: production, client-a)" value={form.tags} onChange={(e) => setForm({ ...form, tags: e.target.value })} />
                        <div className="toolbar">
                            <Button type="submit" loading={saving}>Save</Button>
                            {!active && <Button type="button" variant="danger" onClick={remove}>Delete scan</Button>}
                            {saveMsg && <span className="muted">{saveMsg}</span>}
                        </div>
                    </form>
                </Card>
            </div>

            {!active && (
                <div style={{ marginTop: 18 }}>
                    <Card title={`Findings (${scan.results.length})`}>
                        {scan.results.length === 0 && scan.status === 'Completed' && (
                            <p className="muted">No issues were found by the checks in this profile.</p>
                        )}
                        <div className="chips">
                            {counts.map((c) => (
                                <span key={c.s} className={`sev sev-${c.s.toLowerCase()}`}>{c.n} {c.s}</span>
                            ))}
                        </div>

                        {scan.status === 'Completed' && (
                            <div className="ai-box" style={{ marginBottom: 14 }}>
                                {summary ? (
                                    <>
                                        <h4>Executive summary <Badge>{summary.source === 'ai' ? 'AI' : 'Built-in'}</Badge></h4>
                                        <p>{summary.summary}</p>
                                        {summary.priorities.length > 0 && (
                                            <>
                                                <h4>Priorities</h4>
                                                <ol>{summary.priorities.map((p, i) => <li key={i}>{p}</li>)}</ol>
                                            </>
                                        )}
                                        <small className="muted">{summary.disclaimer}</small>
                                    </>
                                ) : (
                                    <Button variant="ghost" onClick={summarize} loading={summarizing}>Summarize this scan</Button>
                                )}
                            </div>
                        )}

                        {scan.results.map((r) => {
                            const ex = explanations[r.id];
                            return (
                                <details key={r.id} className="finding">
                                    <summary>
                                        <span className={`sev sev-${r.severity.toLowerCase()}`}>{r.severity}</span>
                                        <strong>{r.title}</strong>
                                        <span className="muted">{r.code}</span>
                    
                                    </summary>
                                    <div className="finding-body">
                                        <p>{r.description}</p>
                                        <h4>Evidence</h4>
                                        <pre className="evidence">{r.evidence}</pre>
                                        <p><strong>Affected URL:</strong> {r.affectedUrl}</p>
                                        <h4>Impact</h4>
                                        <p>{r.impact}</p>
                                        <h4>Root cause</h4>
                                        <p>{r.rootCause}</p>
                                        <h4>Recommendation</h4>
                                        <p>{r.recommendation}</p>
                                        <h4>References</h4>
                                        {r.references.map((ref) => (
                                            <div key={ref}><a href={ref} target="_blank" rel="noopener noreferrer">{ref}</a></div>
                                        ))}

                                        <div className="ai-box">
                                            {ex ? (
                                                <>
                                                    <h4>Explanation <Badge>{ex.source === 'ai' ? 'AI' : 'Built-in'}</Badge></h4>
                                                    <p>{ex.summary}</p>
                                                    <h4>Why it matters</h4>
                                                    <p>{ex.whyItMatters}</p>
                                                    <h4>How to fix</h4>
                                                    <ol>{ex.fixSteps.map((s, i) => <li key={i}>{s}</li>)}</ol>
                                                    <p className="muted">{ex.caveats}</p>
                                                    <small className="muted">{ex.disclaimer}</small>
                                                </>
                                            ) : (
                                                <Button variant="ghost" onClick={() => explain(r.id)} loading={explaining === r.id}>Explain this finding</Button>
                                            )}
                                        </div>
                                    </div>
                                </details>
                            );
                        })}
                    </Card>
                </div>
            )}

            {scan.status === 'Completed' && (
                <div style={{ marginTop: 18 }}>
                    <AiChat scanId={scan.id} results={scan.results} />
                </div>
            )}
        </>
    );
}






