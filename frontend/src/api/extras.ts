import { api, apiBlob, post } from './client';

function save(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function downloadExport(scanId: string, format: 'csv' | 'json') {
    const { blob, fileName } = await apiBlob(`/api/scans/${scanId}/export?format=${format}`);
    save(blob, fileName);
}

export const emailAdminApi = {
    status: () => api<{ configured: boolean }>('/api/admin/email/status'),
    test: (to?: string) => post<{ ok: boolean; message: string }>('/api/admin/email/test', { to: to?.trim() || undefined })
};

export const aiFeedbackApi = {
    send: (helpful: boolean, source: string) => post('/api/ai/feedback', { helpful, source })
};
export interface ActivityItem { action: string; createdAtUtc: string }

export const accountApi = {
    activity: () => api<ActivityItem[]>('/api/users/activity'),
    signOutEverywhere: () => post('/api/users/sign-out-everywhere')
};

export interface DbObject { kind: string; name: string; description: string }
export interface ScoreMismatch { scanId: string; stored: number | null; calculated: number }
export interface ScoreCheck { checked: number; mismatchCount: number; mismatches: ScoreMismatch[] }

export const dbToolsApi = {
    objects: () => api<DbObject[]>('/api/admin/db/objects'),
    scoreCheck: () => api<ScoreCheck>('/api/admin/db/score-check'),
    recalculate: () => post<{ updated: number }>('/api/admin/db/recalculate-scores'),
    cleanup: () => post('/api/admin/db/cleanup')
};