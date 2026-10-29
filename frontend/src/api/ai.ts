import { api, post } from './client';

export interface AiExplanation {
    resultId: string; summary: string; whyItMatters: string; fixSteps: string[];
    caveats: string; source: string; disclaimer: string;
}
export interface AiSummary { summary: string; priorities: string[]; source: string; disclaimer: string }
export interface AiMessage { role: string; content: string; createdAtUtc: string }
export interface AiChatReply { conversationId: string; reply: string; source: string; disclaimer: string }
export interface AiAskReply { reply: string; source: string; contextLabel: string; suggestions: string[]; disclaimer: string }

export const aiApi = {
    status: () => api<{ configured: boolean }>('/api/ai/status'),
    explain: (scanId: string, resultId: string) => post<AiExplanation>(`/api/scans/${scanId}/findings/${resultId}/explain`),
    summary: (scanId: string) => post<AiSummary>(`/api/scans/${scanId}/ai/summary`),
    chat: (scanId: string, message: string, resultId?: string) =>
        post<AiChatReply>(`/api/scans/${scanId}/ai/chat`, { message, resultId: resultId || null }),
    conversation: (scanId: string) => api<AiMessage[]>(`/api/scans/${scanId}/ai/conversation`),
    clear: (scanId: string) => api(`/api/scans/${scanId}/ai/conversation`, { method: 'DELETE' }),
    ask: (message: string, scanId: string | undefined, history: { role: string; content: string }[]) =>
        post<AiAskReply>('/api/ai/ask', { message, scanId: scanId ?? null, history })
};