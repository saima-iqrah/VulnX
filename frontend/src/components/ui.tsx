import { useId, useState } from 'react';
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react';

export function Spinner() {
    return <span className="spinner" aria-label="Loading" />;
}

export function PageSpinner() {
    return (
        <div className="page-center">
            <Spinner />
        </div>
    );
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: 'primary' | 'ghost' | 'danger';
    loading?: boolean;
    block?: boolean;
};

export function Button({ variant = 'primary', loading, block, children, disabled, className = '', ...rest }: ButtonProps) {
    return (
        <button
            className={`btn btn-${variant} ${block ? 'btn-block' : ''} ${className}`}
            disabled={disabled || loading}
            {...rest}
        >
            {loading ? <Spinner /> : children}
        </button>
    );
}

type FieldProps = InputHTMLAttributes<HTMLInputElement> & { label: string; hint?: string };

export function Field({ label, hint, type, ...rest }: FieldProps) {
    const id = useId();
    const [visible, setVisible] = useState(false);
    const isPassword = type === 'password';

    return (
        <div className="field">
            <label htmlFor={id}>{label}</label>
            <div className={isPassword ? 'input-wrap' : undefined}>
                <input id={id} className="input" type={isPassword && visible ? 'text' : type} spellCheck={isPassword ? false : undefined} autoCapitalize={isPassword ? 'off' : undefined} {...rest} />
                {isPassword && (
                    <button
                        type="button"
                        className="pw-toggle"
                        onClick={() => setVisible((v) => !v)}
                        aria-label={visible ? 'Hide password' : 'Show password'}
                        aria-pressed={visible}
                    >
                        {visible ? 'Hide' : 'Show'}
                    </button>
                )}
            </div>
            {hint && <small>{hint}</small>}
        </div>
    );
}

export function Alert({ kind = 'info', children }: { kind?: 'error' | 'success' | 'info'; children: ReactNode }) {
    return (
        <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
            {children}
        </div>
    );
}

export function Card({ title, children, className = '' }: { title?: string; children: ReactNode; className?: string }) {
    return (
        <section className={`card ${className}`}>
            {title && <h3 className="card-title">{title}</h3>}
            {children}
        </section>
    );
}

export function Badge({ children, tone = 'default' }: { children: ReactNode; tone?: 'default' | 'ok' | 'warn' | 'danger' }) {
    return <span className={`badge badge-${tone}`}>{children}</span>;
}

export function StatCard({ label, value, note }: { label: string; value: string; note?: string }) {
    return (
        <div className="card stat">
            <span className="stat-label">{label}</span>
            <strong className="stat-value">{value}</strong>
            {note && <span className="stat-note">{note}</span>}
        </div>
    );
}

export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
    return (
        <header className="page-header">
            <h1>{title}</h1>
            {subtitle && <p>{subtitle}</p>}
        </header>
    );
}