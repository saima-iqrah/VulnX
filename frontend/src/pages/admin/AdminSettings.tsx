import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Alert, Badge, Button, Card, Field, PageHeader, Spinner } from '../../components/ui';
import { adminApi } from '../../api/platform';
import { emailAdminApi } from '../../api/extras';
import type { SettingItem } from '../../api/platform';

type Msg = { kind: 'success' | 'error'; text: string } | null;

export default function AdminSettings() {
    const [settings, setSettings] = useState<SettingItem[] | null>(null);
    const [values, setValues] = useState<Record<string, string>>({});
    const [msg, setMsg] = useState<Msg>(null);
    const [saving, setSaving] = useState(false);

    const [title, setTitle] = useState('');
    const [message, setMessage] = useState('');
    const [annMsg, setAnnMsg] = useState<Msg>(null);
    const [sending, setSending] = useState(false);

    const [emailConfigured, setEmailConfigured] = useState<boolean | null>(null);
    const [emailMsg, setEmailMsg] = useState<Msg>(null);
    const [testing, setTesting] = useState(false);
    const [testTo, setTestTo] = useState('');

    useEffect(() => {
        let on = true;
        adminApi.settings()
            .then((s) => { if (on) { setSettings(s); setValues(Object.fromEntries(s.map((x) => [x.key, x.value]))); } })
            .catch((e) => { if (on) setMsg({ kind: 'error', text: e instanceof Error ? e.message : 'Could not load settings.' }); });
        emailAdminApi.status().then((s) => { if (on) setEmailConfigured(s.configured); }).catch(() => undefined);
        return () => { on = false; };
    }, []);

    const save = async (e: FormEvent) => {
        e.preventDefault();
        setSaving(true);
        setMsg(null);
        try {
            await adminApi.saveSettings(values);
            setMsg({ kind: 'success', text: 'Settings saved. They apply within about 30 seconds.' });
        } catch (err) {
            setMsg({ kind: 'error', text: err instanceof Error ? err.message : 'Could not save settings.' });
        } finally {
            setSaving(false);
        }
    };

    const announce = async (e: FormEvent) => {
        e.preventDefault();
        if (!window.confirm('Send this announcement to every active user?')) return;
        setSending(true);
        setAnnMsg(null);
        try {
            const r = await adminApi.announce(title, message);
            setAnnMsg({ kind: 'success', text: `Sent to ${r.sent} user(s).` });
            setTitle('');
            setMessage('');
        } catch (err) {
            setAnnMsg({ kind: 'error', text: err instanceof Error ? err.message : 'Could not send the announcement.' });
        } finally {
            setSending(false);
        }
    };

    const testEmail = async () => {
        setTesting(true);
        setEmailMsg(null);
        try {
            const r = await emailAdminApi.test(testTo);
            setEmailMsg({ kind: r.ok ? 'success' : 'error', text: r.message });
        } catch (err) {
            setEmailMsg({ kind: 'error', text: err instanceof Error ? err.message : 'The test failed.' });
        } finally {
            setTesting(false);
        }
    };

    return (
        <>
            <PageHeader title="System settings" subtitle="Platform limits, email delivery and announcements." />
            <div className="grid2">
                <Card title="Settings">
                    {msg && <Alert kind={msg.kind}>{msg.text}</Alert>}
                    {!settings && !msg && <Spinner />}
                    {settings && (
                        <form onSubmit={save} className="form">
                            {settings.map((s) => (
                                <div key={s.key} className="field">
                                    <label htmlFor={s.key}>{s.key}</label>
                                    {s.key === 'registration.enabled' ? (
                                        <select id={s.key} className="input" value={values[s.key]} onChange={(e) => setValues({ ...values, [s.key]: e.target.value })}>
                                            <option value="true">true</option>
                                            <option value="false">false</option>
                                        </select>
                                    ) : (
                                        <input id={s.key} className="input" type="number" value={values[s.key]} onChange={(e) => setValues({ ...values, [s.key]: e.target.value })} />
                                    )}
                                    <small>{s.description} Default: {s.defaultValue}.</small>
                                </div>
                            ))}
                            <Button type="submit" loading={saving}>Save settings</Button>
                        </form>
                    )}
                </Card>

                <Card title="Send an announcement">
                    {annMsg && <Alert kind={annMsg.kind}>{annMsg.text}</Alert>}
                    <form onSubmit={announce} className="form">
                        <Field label="Title" maxLength={120} required value={title} onChange={(e) => setTitle(e.target.value)} />
                        <div className="field">
                            <label htmlFor="ann-msg">Message</label>
                            <textarea id="ann-msg" className="input" maxLength={1000} required value={message} onChange={(e) => setMessage(e.target.value)} />
                        </div>
                        <Button type="submit" loading={sending}>Send to all users</Button>
                    </form>
                </Card>
            </div>

            <div style={{ marginTop: 18 }}>
                <Card title="Email delivery">
                    <p className="muted">
                        Status:{' '}
                        {emailConfigured === null ? <Spinner /> : emailConfigured
                            ? <Badge tone="ok">SMTP configured</Badge>
                            : <Badge tone="warn">Not configured: emails are only written to the API log</Badge>}
                    </p>
                    <p className="muted">Sends a test message and shows the exact error if it fails.</p>
                    {emailMsg && <Alert kind={emailMsg.kind}>{emailMsg.text}</Alert>}
                    <div style={{ marginTop: 12 }}>
                        <Field
                            label="Send test to"
                            type="email"
                            name="test-recipient"
                            autoComplete="off"
                            autoCapitalize="off"
                            spellCheck={false}
                            placeholder="Leave empty to use your admin email"
                            value={testTo}
                            onChange={(e) => setTestTo(e.target.value)}
                        />
                    </div>
                    <div className="toolbar" style={{ marginTop: 12 }}>
                        <Button type="button" onClick={testEmail} loading={testing}>Send test email</Button>
                    </div>
                </Card>
            </div>
        </>
    );
}