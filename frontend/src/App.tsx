import { Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import Landing from './pages/Landing';
import Login from './pages/Login';
import AdminLogin from './pages/AdminLogin';
import Register from './pages/Register';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';
import VerifyEmail from './pages/VerifyEmail';
import VerifyReport from './pages/VerifyReport';
import NotFound from './pages/NotFound';
import UserLayout from './pages/user/UserLayout';
import UserOverview from './pages/user/UserOverview';
import Profile from './pages/user/Profile';
import NewScan from './pages/user/NewScan';
import ScanDetail from './pages/user/ScanDetail';
import ScanHistory from './pages/user/ScanHistory';
import Compare from './pages/user/Compare';
import Notifications from './pages/user/Notifications';
import AdminLayout from './pages/admin/AdminLayout';
import AdminOverview from './pages/admin/AdminOverview';
import AdminUsers from './pages/admin/AdminUsers';
import AdminScans from './pages/admin/AdminScans';
import AdminAudit from './pages/admin/AdminAudit';
import AdminSettings from './pages/admin/AdminSettings';

export default function App() {
    return (
        <Routes>
            <Route path="/" element={<Landing />} />
            <Route path="/login" element={<Login />} />
            <Route path="/admin/login" element={<AdminLogin />} />
            <Route path="/register" element={<Register />} />
            <Route path="/forgot-password" element={<ForgotPassword />} />
            <Route path="/reset-password" element={<ResetPassword />} />
            <Route path="/verify-email" element={<VerifyEmail />} />
            <Route path="/verify-report" element={<VerifyReport />} />

            {/* User dashboard (role: User) */}
            <Route path="/app" element={<ProtectedRoute role="User"><UserLayout /></ProtectedRoute>}>
                <Route index element={<UserOverview />} />
                <Route path="scans/new" element={<NewScan />} />
                <Route path="scans/:id" element={<ScanDetail />} />
                <Route path="history" element={<ScanHistory />} />
                <Route path="compare" element={<Compare />} />
                <Route path="notifications" element={<Notifications />} />
                <Route path="profile" element={<Profile />} />
            </Route>

            {/* Admin dashboard (role: Admin) - fully separate from the user dashboard */}
            <Route path="/admin" element={<ProtectedRoute role="Admin"><AdminLayout /></ProtectedRoute>}>
                <Route index element={<AdminOverview />} />
                <Route path="users" element={<AdminUsers />} />
                <Route path="scans" element={<AdminScans />} />
                <Route path="audit" element={<AdminAudit />} />
                <Route path="settings" element={<AdminSettings />} />
            </Route>

            <Route path="*" element={<NotFound />} />
        </Routes>
    );
}