import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Logo } from './Logo';
import { NotificationBell } from './NotificationBell';
import { AiAssistant } from './AiAssistant';
import { ThemeToggle } from './ThemeToggle';
import { Badge, Button } from './ui';

export interface NavItem {
    to: string;
    label: string;
    end?: boolean;
}

/** Shared layout used by BOTH dashboards. Each dashboard supplies its own nav + label, so they stay separate. */
export function DashboardShell({ nav, roleLabel, homePath }: { nav: NavItem[]; roleLabel: string; homePath: string }) {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);

    const handleLogout = async () => {
        await logout();
        navigate(roleLabel === 'Admin' ? '/admin/login' : '/login', { replace: true });
    };

    return (
        <div className="shell">
            <aside className={`sidebar ${open ? 'open' : ''}`}>
                <Logo to={homePath} />
                <Badge tone={roleLabel === 'Admin' ? 'warn' : 'ok'}>{roleLabel} console</Badge>
                <nav className="nav">
                    {nav.map((item) => (
                        <NavLink key={item.to} to={item.to} end={item.end} onClick={() => setOpen(false)}>
                            {item.label}
                        </NavLink>
                    ))}
                </nav>
                <div className="sidebar-foot">
                    <div className="who">
                        <strong>{user?.fullName}</strong>
                        <small>{user?.email}</small>
                    </div>
                    <Button variant="ghost" block onClick={handleLogout}>
                        Sign out
                    </Button>
                </div>
            </aside>
            {open && <div className="scrim" onClick={() => setOpen(false)} />}
            <div className="main">
                <header className="topbar">
                    <button className="menu-btn" onClick={() => setOpen(true)} aria-label="Open menu">
                        &#9776;
                    </button>
                    <span className="muted">Authorized security assessments only</span>
                    <div className="topbar-actions">
                        {roleLabel === 'User' && <NotificationBell />}
                        <ThemeToggle />
                    </div>
                </header>
                <main className="content">
                    <Outlet />
                </main>
            </div>
            {roleLabel === 'User' && <AiAssistant />}
        </div>
    );
}