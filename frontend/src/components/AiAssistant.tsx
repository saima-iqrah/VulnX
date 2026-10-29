import { useEffect, useRef, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { useMatch } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { aiApi } from '../api/ai';
import { aiFeedbackApi } from '../api/extras';

interface Msg { role: 'user' | 'assistant'; content: string; source?: string; rated?: boolean }

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DEFAULT_SUGGESTIONS = ['How do I start a scan?', 'What does the grade mean?', 'What is XSS?'];

function inline(text: string): ReactNode[] {
    return text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g).map((part, i) => {
        if (part.length > 4 && part.startsWith('**') && part.endsWith('**')) return <strong key={i}>{part.slice(2, -2)}</strong>;
        if (part.length > 2 && part.startsWith('`') && part.endsWith('`')) return <code key={i}>{part.slice(1, -1)}</code>;
        return part;
    });
}

/** Renders the small subset of markdown that assistants use (bold, code, bullet lists) without raw HTML. */
function Formatted({ text }: { text: string }) {
    const blocks: ReactNode[] = [];
    let list: string[] = [];
    const flush = () => {
        if (list.length) {
            blocks.push(<ul key={`l${blocks.length}`}>{list.map((l, i) => <li key={i}>{inline(l)}</li>)}</ul>);
            list = [];
        }
    };
    text.split('\n').forEach((line) => {
        const m = /^\s*(?:[-*]|\d+\.)\s+(.*)$/.exec(line);
        if (m) {
            list.push(m[1]);
        } else {
            flush();
            if (line.trim() && line.trim() !== '---') blocks.push(<p key={`p${blocks.length}`}>{inline(line)}</p>);
        }
    });
    flush();
    return <>{blocks}</>;
}

export function AiAssistant() {
    const { user } = useAuth();
    const match = useMatch('/app/scans/:id');
    const scanId = match?.params.id && GUID.test(match.params.id) ? match.params.id : undefined;
    const storageKey = `vulnx.assistant.${user?.id ?? 'anon'}`;

    const [open, setOpen] = useState(false);
    const [messages, setMessages] = useState<Msg[]>(() => {
        try {
            const raw = sessionStorage.getItem(storageKey);
            return raw ? (JSON.parse(raw) as Msg[]) : [];
        } catch {
            return [];
        }
    });
    const [input, setInput] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [context, setContext] = useState('');
    const [suggestions, setSuggestions] = useState<string[]>(DEFAULT_SUGGESTIONS);
    const endRef = useRef<HTMLDivElement>(null);
    const inputRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify(messages.slice(-30)));
        } catch {
            /* storage may be unavailable */
        }
    }, [messages, storageKey]);

    useEffect(() => {
        if (open) endRef.current?.scrollIntoView({ block: 'nearest' });
    }, [open, messages, busy]);

    useEffect(() => {
        if (open) inputRef.current?.focus();
    }, [open]);

    useEffect(() => {
        if (!open) return;
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') setOpen(false);
        };
        window.addEventListener('keydown', onKey);
        return () => window.removeEventListener('keydown', onKey);
    }, [open]);

    const send = async (text: string) => {
        const content = text.trim();
        if (!content || busy) return;
        setError('');
        setInput('');
        setBusy(true);
        const history = messages.slice(-10).map((m) => ({ role: m.role, content: m.content }));
        setMessages((prev) => [...prev, { role: 'user', content }]);
        try {
            const r = await aiApi.ask(content, scanId, history);
            setMessages((prev) => [...prev, { role: 'assistant', content: r.reply, source: r.source }]);
            setContext(r.contextLabel);
            setSuggestions(r.suggestions.length ? r.suggestions : DEFAULT_SUGGESTIONS);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'The assistant could not answer.');
        } finally {
            setBusy(false);
        }
    };

    const rate = (index: number, helpful: boolean) => {
    const m = messages[index];
    if (!m || m.rated) return;
    setMessages((prev) => prev.map((x, i) => (i === index ? { ...x, rated: true } : x)));
    aiFeedbackApi.send(helpful, m.source ?? 'template').catch(() => undefined);
  };

  const submit = (e: FormEvent) => {
        e.preventDefault();
        void send(input);
    };

    const clear = () => {
        setMessages([]);
        setError('');
        try {
            sessionStorage.removeItem(storageKey);
        } catch {
            /* ignore */
        }
    };

    return (
        <>
            {open && (
                <section className="ai-panel" role="dialog" aria-label="VulnX assistant">
                    <header className="ai-head">
                        <div className="grow">
                            <strong>VulnX Assistant</strong>
                            <span className="ai-ctx">{context || (scanId ? 'This scan' : 'Ask me anything')}</span>
                        </div>
                        {messages.length > 0 && <button className="ai-icon-btn" onClick={clear}>Clear</button>}
                        <button className="ai-icon-btn" onClick={() => setOpen(false)} aria-label="Close assistant">&times;</button>
                    </header>

                    <div className="ai-body">
                        {messages.length === 0 && (
                            <div className="ai-msg assistant">
                                <p>Hi! Ask me about your scan results, web security, or how to use VulnX.</p>
                            </div>
                        )}
                        {messages.map((m, i) => (
                            <div key={i} className={`ai-msg ${m.role}`}>
                                {m.role === 'assistant' ? <Formatted text={m.content} /> : <p>{m.content}</p>}
                                {m.role === 'assistant' && m.source && (
                  <span className="ai-src">
                    {m.source === 'ai' ? 'AI model' : 'Built-in knowledge'}
                    {m.rated ? ' - thanks!' : (
                      <>
                        {' - '}
                        <button className="ai-rate" onClick={() => rate(i, true)} aria-label="Helpful">&#128077;</button>
                        <button className="ai-rate" onClick={() => rate(i, false)} aria-label="Not helpful">&#128078;</button>
                      </>
                    )}
                  </span>
                )}
                            </div>
                        ))}
                        {busy && <div className="ai-msg assistant"><p>Thinking...</p></div>}
                        {error && <div className="ai-msg assistant"><p>{error}</p></div>}
                        <div ref={endRef} />
                    </div>

                    <div className="ai-chips">
                        {suggestions.map((s) => (
                            <button key={s} className="ai-chip" onClick={() => void send(s)} disabled={busy}>{s}</button>
                        ))}
                    </div>

                    <form className="ai-form" onSubmit={submit}>
                        <input
                            ref={inputRef}
                            className="input"
                            placeholder="Type your question..."
                            maxLength={1000}
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                        />
                        <button type="submit" className="btn btn-primary" disabled={busy || !input.trim()}>Send</button>
                    </form>
                    <p className="ai-note">Findings come from the scanner. AI can make mistakes, so verify before changing production.</p>
                </section>
            )}

            <button className="ai-fab" onClick={() => setOpen((o) => !o)} aria-label={open ? 'Close assistant' : 'Open assistant'}>
                <svg width="26" height="26" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v8A2.5 2.5 0 0 1 17.5 16H10l-4.2 3.6c-.5.4-1.3.1-1.3-.6V16A2.5 2.5 0 0 1 4 13.5v-8Z" fill="currentColor" />
                    <circle cx="9" cy="9.5" r="1.1" fill="#0b1325" />
                    <circle cx="12" cy="9.5" r="1.1" fill="#0b1325" />
                    <circle cx="15" cy="9.5" r="1.1" fill="#0b1325" />
                </svg>
            </button>
        </>
    );
}



