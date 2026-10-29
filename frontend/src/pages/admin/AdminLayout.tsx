import { DashboardShell } from '../../components/DashboardShell';
import type { NavItem } from '../../components/DashboardShell';

const nav: NavItem[] = [
    { to: '/admin', label: 'Overview', end: true },
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/scans', label: 'All scans' },
    { to: '/admin/audit', label: 'Audit logs' },
    { to: '/admin/settings', label: 'Settings' }
];

export default function AdminLayout() {
    return <DashboardShell nav={nav} roleLabel="Admin" homePath="/admin" />;
}