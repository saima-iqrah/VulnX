import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Alert, Badge, Button, Card } from './ui';
import { aiApi } from '../api/ai';
import type { AiMessage } from '../api/ai';
import type { ScanResult } from '../api/scans';

export function AiChat({ scanId, results }: { scanId: string; results: ScanResult[] }) {
    const [messages, setMessages] = useState<AiMessage[]>([]);
    const [input, setInput] = useState('');
    const [focus, setFocus] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [configured, setConfigured] = useState<boolean | null>(null);
    const endRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        let on = true;
        aiApi.conversation(scanId).then((m) => on && setMessages(m)).catch(() => undefined);
        aiApi.status().then((s) => on && setConfigured(s.configured)).catch(() => undefined);
        return () => { on = false; };
    }, [scanId]);

    useEffect(() => {
        endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }, [messages]);

    const send = async (e: FormEvent) => {
        e.preventDefault();
        const text = input.trim();
        if (!text || busy) return;
        setError('');
        setBusy(true);
        setInput('');
        setMessages((m) => [...m, { role: 'user', content: text, createdAtUtc: new Date().toISOString() }]);
        try {
            const reply = await aiApi.chat(scanId, text, focus || undefined);
            setMessages((m) => [...m, { role: 'assistant', content: reply.reply, createdAtUtc: new Date().toISOString() }]);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'The assistant could not answer.');
        } finally {
            setBusy(false);
        }
    };

    const clear = async () => {
        try {
            await aiApi.clear(scanId);
            setMessages([]);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Could not clear the chat.');
        }
    };

    return (
        <Card title="Ask the security assistant">
            <p className="muted">
                Ask about this scan or any web-security topic. Findings always come from the scanner.{' '}
                {configured === true && <Badge tone="ok">AI connected</Badge>}
                {configured === false && <Badge>Built-in explanations (no AI key set)</Badge>}
            </p>
            {error && <Alert kind="error">{error}</Alert>}

            <div className="chat">
                {messages.length === 0 && <p className="muted">Try: "What should I fix first?" or "Explain the cookie findings."</p>}
                {messages.map((m, i) => (
                    <div key={i} className={`msg ${m.role}`}>{m.content}</div>
                ))}
                {busy && <div className="msg assistant">Thinking...</div>}
                <div ref={endRef} />
            </div>

            <form onSubmit={send} className="chat-form">
                <select className="input" style={{ maxWidth: 240 }} value={focus} onChange={(e) => setFocus(e.target.value)} aria-label="Finding to focus on">
                    <option value="">About the whole scan</option>
                    {results.map((r) => (
                        <option key={r.id} value={r.id}>{r.code} - {r.title}</option>
                    ))}
                </select>
                <input className="input" placeholder="Ask about this scan..." maxLength={1000} value={input} onChange={(e) => setInput(e.target.value)} />
                <Button type="submit" loading={busy} disabled={!input.trim()}>Send</Button>
                {messages.length > 0 && <Button type="button" variant="ghost" onClick={clear}>Clear</Button>}
            </form>
        </Card>
    );
}
