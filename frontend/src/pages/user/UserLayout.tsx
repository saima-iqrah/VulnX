import { DashboardShell } from '../../components/DashboardShell';
import type { NavItem } from '../../components/DashboardShell';

const nav: NavItem[] = [
    { to: '/app', label: 'Overview', end: true },
    { to: '/app/scans/new', label: 'New scan' },
    { to: '/app/history', label: 'Scan history' },
    { to: '/app/compare', label: 'Compare scans' },
    { to: '/app/notifications', label: 'Notifications' },
    { to: '/app/profile', label: 'Profile' }
];

export default function UserLayout() {
    return <DashboardShell nav={nav} roleLabel="User" homePath="/app" />;
}