import { api, post, put } from './client';

export interface ScanModule { name: string; status: string; progressPercent: number; error: string | null }

export interface ScanResult {
    id: string; code: string; title: string; category: string; severity: string; confidence: string;
    description: string; rootCause: string; impact: string; affectedUrl: string; evidence: string;
    recommendation: string; references: string[]; detectionModule: string; detectedAtUtc: string;
}

export interface ScanSummary {
    id: string; target: string; profile: string; status: string; progressPercent: number;
    score: number | null; grade: string | null; findingsCount: number;
    label: string | null; tags: string[]; createdAtUtc: string; completedAtUtc: string | null;
}

export interface ScanDetail {
    id: string; target: string; profile: string; status: string; progressPercent: number;
    score: number | null; grade: string | null; error: string | null;
    label: string | null; notes: string | null; tags: string[];
    createdAtUtc: string; startedAtUtc: string | null; completedAtUtc: string | null;
    modules: ScanModule[]; results: ScanResult[];
}

export interface ScanProgress {
    scanId: string; status: string; progressPercent: number; currentModule: string | null;
    modules: ScanModule[]; findingsCount: number; score: number | null; grade: string | null; error: string | null;
}

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export interface FindingKey { code: string; title: string; severity: string; affectedUrl: string; evidence: string }

export interface ScanCompare {
    base: ScanSummary; target: ScanSummary; scoreDelta: number | null;
    added: FindingKey[]; resolved: FindingKey[]; unchanged: FindingKey[];
}

export interface TrendPoint { scanId: string; date: string; score: number; critical: number; high: number; medium: number; low: number }

export interface Dashboard {
    totalScans: number; completedScans: number; latestScore: number | null; latestGrade: string | null;
    openFindings: number; criticalFindings: number; severityCounts: Record<string, number>;
    trend: TrendPoint[]; recent: ScanSummary[];
}

export interface ListOptions { page?: number; pageSize?: number; status?: string; search?: string; tag?: string }

export const isActive = (status: string) => status === 'Queued' || status === 'Running';

export const scansApi = {
    create: (url: string, profile: string, authorizationConfirmed: boolean) =>
        post<ScanSummary>('/api/scans', { url, profile, authorizationConfirmed }),
    list: (o: ListOptions = {}) => {
        const q = new URLSearchParams();
        q.set('page', String(o.page ?? 1));
        q.set('pageSize', String(o.pageSize ?? 20));
        if (o.status) q.set('status', o.status);
        if (o.search) q.set('search', o.search);
        if (o.tag) q.set('tag', o.tag);
        return api<Paged<ScanSummary>>(`/api/scans?${q.toString()}`);
    },
    get: (id: string) => api<ScanDetail>(`/api/scans/${id}`),
    update: (id: string, body: { label?: string; notes?: string; tags?: string[] }) => put<ScanDetail>(`/api/scans/${id}`, body),
    remove: (id: string) => api(`/api/scans/${id}`, { method: 'DELETE' }),
    cancel: (id: string) => post(`/api/scans/${id}/cancel`),
    compare: (baseId: string, targetId: string) => api<ScanCompare>(`/api/scans/compare?baseId=${baseId}&targetId=${targetId}`),
    dashboard: () => api<Dashboard>('/api/scans/dashboard')
};