import { Link } from 'react-router-dom';
import { Logo } from '../components/Logo';
import { ThemeToggle } from '../components/ThemeToggle';
import { useAuth, homeFor } from '../auth/AuthContext';

const stats = [
    { n: '26', l: 'security checks' },
    { n: '6', l: 'scanner modules' },
    { n: 'A-F', l: 'deterministic score' },
    { n: 'PDF', l: 'verifiable reports' }
];

const features = [
    { t: 'Modular safe scanner', d: 'Passive, low-impact checks: security headers, TLS, cookies, CORS, information disclosure and HTML content. No exploitation, ever.' },
    { t: 'Evidence-based findings', d: 'Every finding stores the exact evidence, severity, confidence and affected URL. The scanner is the source of truth.' },
    { t: 'Deterministic scoring', d: 'A transparent 0-100 score with a documented formula, so two identical scans always give the same grade.' },
    { t: 'AI assistant', d: 'Ask about your results or about web security. It explains stored findings and never invents vulnerabilities.' },
    { t: 'Professional PDF reports', d: 'Executive summary, technical details and a remediation checklist with a verification code anyone can check.' },
    { t: 'History and comparison', d: 'Track progress over time, compare two scans and export findings as CSV or JSON.' }
];

const steps = [
    { n: '01', t: 'Add your target', d: 'Enter a URL you own or are authorized to test and confirm authorization.' },
    { n: '02', t: 'Scan safely', d: 'Rate-limited, scope-enforced, SSRF-protected checks run in the background with live progress.' },
    { n: '03', t: 'Fix with confidence', d: 'Review severity-ranked findings, ask the assistant, and download a verified report.' }
];

const faqs = [
    { q: 'Is it legal to scan any website?', a: 'No. Scan only sites you own or have written permission to test. VulnX asks you to confirm authorization for every scan and blocks private and internal addresses.' },
    { q: 'Will a scan damage or slow down my site?', a: 'No. Scans are passive and low-impact: a small, rate-limited number of normal requests with time limits. There is no exploitation, brute force or denial-of-service.' },
    { q: 'Does the AI find the vulnerabilities?', a: 'No. Findings come only from the scanner. The assistant explains stored findings and answers questions; it never creates findings or changes scores.' },
    { q: 'How is the security score calculated?', a: 'It starts at 100 and subtracts a fixed penalty per finding type (critical 25, high 15, medium 7, low 3). The same findings always give the same score.' },
    { q: 'How can I check that a PDF report is genuine?', a: 'Every report has a number and a verification code. Enter them on the Verify report page; the check fails if anything was changed.' }
];

/** Large faint shield shown on the right half of desktop screens. */
function Watermark() {
    return (
        <div className="watermark" aria-hidden="true">
            <svg viewBox="0 0 400 460" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g className="ring" stroke="currentColor" strokeWidth="1.5" strokeDasharray="6 10">
                    <circle cx="200" cy="230" r="195" />
                    <circle cx="200" cy="230" r="160" />
                </g>
                <path d="M200 40 62 92v108c0 92 58 162 138 198 80-36 138-106 138-198V92L200 40Z" stroke="currentColor" strokeWidth="8" strokeLinejoin="round" />
                <path d="M200 76 92 116v84c0 74 44 130 108 160 64-30 108-86 108-160v-84L200 76Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" opacity=".7" />
                <path d="m138 214 46 46 82-96" stroke="currentColor" strokeWidth="16" strokeLinecap="round" strokeLinejoin="round" />
                <g stroke="currentColor" strokeWidth="2" opacity=".6">
                    <path d="M200 40V10M62 92 30 78M338 92l32-14" />
                    <circle cx="200" cy="8" r="5" fill="currentColor" />
                </g>
            </svg>
        </div>
    );
}

export default function Landing() {
    const { user } = useAuth();

    return (
        <div className="landing">
            <Watermark />

            <header className="lp-nav container">
                <Logo />
                <nav>
                    <a href="#features">Features</a>
                    <a href="#how">How it works</a>
                    <a href="#faq">FAQ</a>
                    <Link to="/verify-report">Verify report</Link>
                    <ThemeToggle />
                    {user ? (
                        <Link className="btn btn-primary" to={homeFor(user)}>Open dashboard</Link>
                    ) : (
                        <>
                            <Link to="/login">Sign in</Link>
                            <Link className="btn btn-primary" to="/register">Get started</Link>
                        </>
                    )}
                </nav>
            </header>

            <section className="hero2 container">
                <div className="hero2-copy">
                    <span className="eyebrow">Defensive security, made simple</span>
                    <h1>
                        Find web vulnerabilities <span className="grad">before attackers do.</span>
                    </h1>
                    <p className="lead">
                        VulnX is an AI-assisted vulnerability assessment platform. Scan the sites you are authorized to test, get
                        evidence-backed findings, a clear security score and ready-to-share, verifiable reports.
                    </p>
                    <div className="hero-cta">
                        <Link className="btn btn-primary btn-lg" to="/register">Start a free assessment</Link>
                        <a className="btn btn-ghost btn-lg" href="#how">See how it works</a>
                    </div>
                    <div className="trust">
                        <span>Passive and safe</span>
                        <span>SSRF protected</span>
                        <span>Authorized use only</span>
                    </div>
                </div>

                <div className="hero2-visual">
                    <div className="report-card" aria-hidden="true">
                        <div className="rc-head"><span>example.com</span><span className="rc-grade">B</span></div>
                        <div className="hc-row"><span className="dot ok" /> Content-Security-Policy <em>present</em></div>
                        <div className="hc-row"><span className="dot warn" /> Strict-Transport-Security <em>weak max-age</em></div>
                        <div className="hc-row"><span className="dot bad" /> Cookie "session" <em>missing HttpOnly</em></div>
                        <div className="hc-row"><span className="dot ok" /> TLS certificate <em>valid, 74 days</em></div>
                        <div className="rc-score">
                            <div className="progress"><span style={{ width: '78%' }} /></div>
                            <small>Security score 78/100</small>
                        </div>
                    </div>
                </div>
            </section>

            <section className="container stats-strip">
                {stats.map((s) => (
                    <div key={s.l} className="stat-chip"><b>{s.n}</b><span>{s.l}</span></div>
                ))}
            </section>

            <section id="features" className="container section">
                <h2>Everything you need for a clear security picture</h2>
                <div className="grid3">
                    {features.map((f) => (
                        <article key={f.t} className="card feature">
                            <h3>{f.t}</h3>
                            <p>{f.d}</p>
                        </article>
                    ))}
                </div>
            </section>

            <section id="how" className="container section">
                <h2>How it works</h2>
                <div className="grid3">
                    {steps.map((s) => (
                        <article key={s.n} className="card step">
                            <span className="step-n">{s.n}</span>
                            <h3>{s.t}</h3>
                            <p>{s.d}</p>
                        </article>
                    ))}
                </div>
            </section>

            <section className="container section">
                <div className="split">
                    <div>
                        <h2>An assistant that knows your scan</h2>
                        <p className="muted">Open the chat bubble on any page and ask what to fix first, what a finding means, or how HSTS works. Answers use your stored findings, so nothing is invented.</p>
                        <p className="muted">Works out of the box with a built-in knowledge base, or connect a local or cloud AI model for open-ended questions.</p>
                    </div>
                    <div className="mock-chat" aria-hidden="true">
                        <div className="ai-msg user"><p>What should I fix first?</p></div>
                        <div className="ai-msg assistant">
                            <p>Start with these:</p>
                            <ul>
                                <li><strong>High</strong> TLS-002: renew the expired certificate</li>
                                <li><strong>Medium</strong> HDR-001: add a Content-Security-Policy</li>
                            </ul>
                            <p>After fixing, run the scan again to confirm.</p>
                        </div>
                    </div>
                </div>
            </section>

            <section id="safety" className="container section">
                <div className="card safety">
                    <h2>Built for authorized, defensive use</h2>
                    <ul>
                        <li>Every scan requires explicit confirmation that you are authorized to test the target.</li>
                        <li>Scope enforcement and SSRF protection block internal and private network targets.</li>
                        <li>Rate limits and timeouts keep scans gentle. No exploitation, brute force or denial-of-service.</li>
                        <li>Findings come only from the scanner. AI explains them; it never creates new ones.</li>
                    </ul>
                </div>
            </section>

            <section id="faq" className="container section">
                <h2>Frequently asked questions</h2>
                {faqs.map((f) => (
                    <details key={f.q} className="faq">
                        <summary>{f.q}</summary>
                        <p>{f.a}</p>
                    </details>
                ))}
            </section>

            <section className="container section">
                <div className="card cta">
                    <h2>Ready to see how secure your site is?</h2>
                    <p>Create a free account and run your first scan in under a minute.</p>
                    <Link className="btn btn-primary btn-lg" to={user ? homeFor(user) : '/register'}>{user ? 'Open dashboard' : 'Get started'}</Link>
                </div>
            </section>

            <footer className="lp-footer container">
                <Logo />
                <div className="lp-foot-links">
                    <Link to="/verify-report">Verify a report</Link>
                    <Link to="/login">Sign in</Link>
                    <Link to="/admin/login">Admin</Link>
                </div>
                <small>&copy; {new Date().getFullYear()} VulnX. Use only on systems you own or have written permission to test.</small>
            </footer>
        </div>
    );
}