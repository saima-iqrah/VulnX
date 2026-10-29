import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { refreshSession, setAccessToken } from '../api/client';
import { authApi } from '../api/auth';
import type { User } from '../types';

export type Portal = 'User' | 'Admin';

interface AuthState {
    user: User | null;
    loading: boolean;
    login: (email: string, password: string, portal: Portal) => Promise<User>;
    logout: () => Promise<void>;
    setUser: (user: User) => void;
}

const AuthContext = createContext<AuthState | null>(null);

export const isAdmin = (u: User | null) => !!u && u.roles.includes('Admin');
export const homeFor = (u: User | null) => (isAdmin(u) ? '/admin' : '/app');

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [loading, setLoading] = useState(true);

    // On first load try to restore the session from the refresh cookie.
    useEffect(() => {
        let active = true;
        refreshSession().then((session) => {
            if (!active) return;
            setUser(session?.user ?? null);
            setLoading(false);
        });
        return () => {
            active = false;
        };
    }, []);

    const login = useCallback(async (email: string, password: string, portal: Portal) => {
        const res = await authApi.login(email, password);
        const admin = res.user.roles.includes('Admin');

        // Wrong portal: end the session that was just created and never expose it to the UI.
        if ((portal === 'Admin') !== admin) {
            try {
                await authApi.logout();
            } catch {
                /* ignore */
            }
            throw new Error(
                portal === 'Admin'
                    ? 'This account is not an administrator. Please use the regular sign in page.'
                    : 'Administrator accounts must sign in on the admin sign in page.'
            );
        }

        setAccessToken(res.accessToken);
        setUser(res.user);
        return res.user;
    }, []);

    const logout = useCallback(async () => {
        try {
            await authApi.logout();
        } catch {
            /* even if the call fails, forget the session locally */
        }
        setAccessToken(null);
        setUser(null);
    }, []);

    const value = useMemo(() => ({ user, loading, login, logout, setUser }), [user, loading, login, logout]);
    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
    return ctx;
}