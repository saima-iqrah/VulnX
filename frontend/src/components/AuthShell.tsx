import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Logo } from './Logo';
import { ThemeToggle } from './ThemeToggle';

export function AuthShell({ title, subtitle, children, footer }: { title: string; subtitle?: string; children: ReactNode; footer?: ReactNode }) {
    return (
        <div className="auth-page">
            <nav className="auth-nav" aria-label="Site">
                <Link to="/">&larr; Home</Link>
                <ThemeToggle />
            </nav>
            <div className="auth-card">
                <Logo />
                <h1>{title}</h1>
                {subtitle && <p className="muted">{subtitle}</p>}
                {children}
                {footer && <div className="auth-footer">{footer}</div>}
            </div>
        </div>
    );
}