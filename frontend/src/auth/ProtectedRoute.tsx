import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { homeFor, useAuth } from './AuthContext';
import { PageSpinner } from '../components/ui';

export function ProtectedRoute({ role, children }: { role: 'User' | 'Admin'; children: ReactNode }) {
    const { user, loading } = useAuth();
    const location = useLocation();

    if (loading) return <PageSpinner />;
    if (!user) {
        const loginPath = role === 'Admin' ? '/admin/login' : '/login';
        return <Navigate to={loginPath} replace state={{ from: location.pathname }} />;
    }
    if (!user.roles.includes(role)) return <Navigate to={homeFor(user)} replace />;
    return <>{children}</>;
}
