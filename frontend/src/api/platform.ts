import { api, apiBlob, post, put } from './client';
import type { Paged } from './scans';

const qs = (o: Record<string, string | number | undefined>) => {
    const q = new URLSearchParams();
    Object.entries(o).forEach(([k, v]) => {
        if (v !== undefined && v !== '') q.set(k, String(v));
    });
    return q.toString();
};

// ---------------- reports ----------------
export interface ReportInfo { scanId: string; reportNumber: string; verificationCode: string; generatedAtUtc: string; verifyUrl: string }
export interface VerifyResult {
    valid: boolean; message: string; reportNumber: string | null; host: string | null; generatedAtUtc: string | null;
    score: number | null; grade: string | null; findings: number | null; critical: number | null; high: number | null;
}

export const reportsApi = {
    info: (scanId: string) => post<ReportInfo>(`/api/scans/${scanId}/report`),
    download: async (scanId: string) => {
        const { blob, fileName } = await apiBlob(`/api/scans/${scanId}/report/pdf`);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    verify: (number: string, code: string) => api<VerifyResult>(`/api/reports/verify?${qs({ number, code })}`)
};

// ---------------- notifications ----------------
export interface NotificationItem { id: string; type: string; title: string; message: string; createdAtUtc: string; isRead: boolean }

export const notificationsApi = {
    list: (page = 1, unreadOnly = false) => api<Paged<NotificationItem>>(`/api/notifications?${qs({ page, pageSize: 20, unreadOnly: unreadOnly ? 'true' : undefined })}`),
    unreadCount: () => api<{ count: number }>('/api/notifications/unread-count'),
    markRead: (id: string) => post(`/api/notifications/${id}/read`),
    markAllRead: () => post('/api/notifications/read-all'),
    remove: (id: string) => api(`/api/notifications/${id}`, { method: 'DELETE' })
};

// ---------------- admin ----------------
export interface AdminUser {
    id: string; email: string; fullName: string; status: string; isAdmin: boolean; emailConfirmed: boolean;
    scanCount: number; createdAtUtc: string; lastLoginAtUtc: string | null;
}
export interface AdminScan {
    id: string; target: string; ownerEmail: string; profile: string; status: string; score: number | null;
    grade: string | null; findingsCount: number; label: string | null; createdAtUtc: string;
}
export interface AuditEntry {
    id: string; userId: string | null; userEmail: string | null; action: string; entityType: string;
    entityId: string | null; metadata: string | null; createdAtUtc: string;
}
export interface SettingItem { key: string; value: string; defaultValue: string; description: string }
export interface AdminOverview {
    users: number; suspendedUsers: number; admins: number; newUsers7d: number; scans: number; scans24h: number;
    runningScans: number; failedScans24h: number; criticalFindings: number; auditEvents24h: number;
    scansPerDay: { date: string; count: number }[];
}
export interface SystemHealth {
    status: string; databaseOk: boolean; databaseLatencyMs: number; queuedScans: number; runningScans: number;
    lastCompletedScanUtc: string | null; aiConfigured: boolean; emailConfigured: boolean; environment: string;
    version: string; uptimeSeconds: number; memoryMb: number; serverTimeUtc: string;
}

export const adminApi = {
    overview: () => api<AdminOverview>('/api/admin/overview'),
    health: () => api<SystemHealth>('/api/admin/health'),
    users: (o: { page?: number; pageSize?: number; search?: string; status?: string; role?: string }) =>
        api<Paged<AdminUser>>(`/api/admin/users?${qs(o)}`),
    setStatus: (id: string, status: string) => post(`/api/admin/users/${id}/status`, { status }),
    setRole: (id: string, admin: boolean) => post(`/api/admin/users/${id}/role`, { admin }),
    deleteUser: (id: string) => api(`/api/admin/users/${id}`, { method: 'DELETE' }),
    scans: (o: { page?: number; pageSize?: number; search?: string; status?: string }) =>
        api<Paged<AdminScan>>(`/api/admin/scans?${qs(o)}`),
    deleteScan: (id: string) => api(`/api/admin/scans/${id}`, { method: 'DELETE' }),
    audit: (o: { page?: number; pageSize?: number; search?: string }) => api<Paged<AuditEntry>>(`/api/admin/audit?${qs(o)}`),
    settings: () => api<SettingItem[]>('/api/admin/settings'),
    saveSettings: (values: Record<string, string>) => put('/api/admin/settings', values),
    announce: (title: string, message: string) => post<{ sent: number }>('/api/admin/announcements', { title, message })
};