import { Link } from 'react-router-dom';

export function Logo({ to = '/' }: { to?: string }) {
    return (
        <Link to={to} className="logo" aria-label="VulnX home">
            <svg width="26" height="26" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M12 2 4 5v6c0 5 3.4 9.3 8 11 4.6-1.7 8-6 8-11V5l-8-3Z" stroke="url(#g)" strokeWidth="1.8" />
                <path d="m8.5 12 2.5 2.5L16 9.5" stroke="url(#g)" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                <defs>
                    <linearGradient id="g" x1="4" y1="2" x2="20" y2="22">
                        <stop stopColor="#38e8c6" />
                        <stop offset="1" stopColor="#4f8cff" />
                    </linearGradient>
                </defs>
            </svg>
            <span>
                Vuln<b>X</b>
            </span>
        </Link>
    );
}
