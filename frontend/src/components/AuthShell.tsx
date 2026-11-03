import type { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Logo } from './Logo';
import { ThemeToggle } from './ThemeToggle';

type Mode = 'signin' | 'signup' | 'admin' | 'help';

const copy: Record<Mode, { heading: string; text: string; cta: string; to: string; points: string[] }> = {
    signin: {
        heading: 'New to VulnX?',
        text: 'Create an account and start assessing the sites you are authorized to test.',
        cta: 'Sign up',
        to: '/register',
        points: ['Evidence-based findings', 'AI explanations in plain language', 'Verifiable PDF reports']
    },
    signup: {
        heading: 'Welcome back!',
        text: 'Already have an account? Sign in to continue to your workspace.',
        cta: 'Sign in',
        to: '/login',
        points: ['Evidence-based findings', 'AI explanations in plain language', 'Verifiable PDF reports']
    },
    admin: {
        heading: 'Admin console',
        text: 'Restricted to platform administrators. Every action is recorded in the audit log.',
        cta: 'User sign in',
        to: '/login',
        points: ['Role-based access', 'Audit-logged actions', 'System health and settings']
    },
    help: {
        heading: 'Secure by design',
        text: 'VulnX is built for authorized, low-impact security assessments.',
        cta: 'Back to sign in',
        to: '/login',
        points: ['Evidence-based findings', 'AI explanations in plain language', 'Verifiable PDF reports']
    }
};

function modeFor(path: string): Mode {
    if (path.startsWith('/admin')) return 'admin';
    if (path.startsWith('/register')) return 'signup';
    if (path.startsWith('/login')) return 'signin';
    return 'help';
}

function UserIcon() {
    return (
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="12" cy="8" r="4" />
            <path d="M4 21c0-4 3.6-7 8-7s8 3 8 7" />
        </svg>
    );
}

function ShieldIcon() {
    return (
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M12 2 4 5v6c0 5 3.4 9.3 8 11 4.6-1.7 8-6 8-11V5l-8-3Z" />
            <path d="M9.5 12l2 2 3.5-4" />
        </svg>
    );
}

function CheckIcon() {
    return (
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="m5 12.5 4.5 4.5L19 7.5" />
        </svg>
    );
}

export function AuthShell({ title, subtitle, children, footer }: { title: string; subtitle?: string; children: ReactNode; footer?: ReactNode }) {
    const { pathname } = useLocation();
    const mode = modeFor(pathname);
    const c = copy[mode];
    const showTabs = pathname === '/login' || pathname === '/admin/login';

    return (
        <div className={`av-stage av-${mode}`}>
            <nav className="av-nav" aria-label="Site">
                <Link to="/" className="av-home">&larr; Home</Link>
                <ThemeToggle />
            </nav>

            <div className={`av-card ${mode === 'signup' ? 'av-card--left' : 'av-card--right'}`}>
                <aside className="av-overlay">
                    <div className="av-brand">
                        <svg width="30" height="30" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                            <path d="M12 2 4 5v6c0 5 3.4 9.3 8 11 4.6-1.7 8-6 8-11V5l-8-3Z" stroke="currentColor" strokeWidth="1.8" />
                            <path d="m8.5 12 2.5 2.5L16 9.5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                        <span>Vuln<b>X</b></span>
                    </div>

                    <div className="av-overlay-body">
                        <h2>{c.heading}</h2>
                        <p>{c.text}</p>
                        <Link to={c.to} className="av-ghost">{c.cta}</Link>
                        <ul className="av-points">
                            {c.points.map((p) => (
                                <li key={p}><CheckIcon />{p}</li>
                            ))}
                        </ul>
                    </div>

                    <small className="av-overlay-foot">Authorized security assessments only</small>
                </aside>

                <section className="av-pane">
                    <div className="av-mlogo"><Logo /></div>

                    {showTabs && (
                        <div className="av-tabs" role="tablist" aria-label="Sign in as">
                            <Link role="tab" aria-selected={pathname === '/login'} to="/login" className={`av-tab ${pathname === '/login' ? 'is-active' : ''}`}>
                                <UserIcon />User sign in
                            </Link>
                            <Link role="tab" aria-selected={pathname === '/admin/login'} to="/admin/login" className={`av-tab is-admin ${pathname === '/admin/login' ? 'is-active' : ''}`}>
                                <ShieldIcon />Admin sign in
                            </Link>
                        </div>
                    )}

                    <div className="av-head">
                        <h1>{title}</h1>
                        {subtitle && <p className="muted">{subtitle}</p>}
                    </div>

                    {children}
                    {footer && <div className="av-footer">{footer}</div>}
                </section>
            </div>
        </div>
    );
}