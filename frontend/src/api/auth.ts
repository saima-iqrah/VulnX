import { api, post, put } from './client';
import type { AuthResponse, User } from '../types';

export const authApi = {
    register: (fullName: string, email: string, password: string) =>
        post<User>('/api/auth/register', { fullName, email, password }),
    login: (email: string, password: string) => post<AuthResponse>('/api/auth/login', { email, password }),
    logout: () => post('/api/auth/logout'),
    forgotPassword: (email: string) => post('/api/auth/forgot-password', { email }),
    resetPassword: (email: string, token: string, newPassword: string) =>
        post('/api/auth/reset-password', { email, token, newPassword }),
    confirmEmail: (email: string, token: string) => post('/api/auth/confirm-email', { email, token }),
    me: () => api<User>('/api/auth/me'),
    updateProfile: (fullName: string) => put<User>('/api/users/profile', { fullName }),
    changePassword: (currentPassword: string, newPassword: string) =>
        post('/api/users/change-password', { currentPassword, newPassword })
};

export const systemApi = {
    adminPing: () => api<{ message: string }>('/api/admin/ping'),
    health: async (): Promise<string> => {
        const res = await fetch('/api/health');
        return (await res.text()) || String(res.status);
    }
};
