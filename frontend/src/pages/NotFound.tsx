import { Link } from 'react-router-dom';
import { AuthShell } from '../components/AuthShell';

export default function NotFound() {
    return (
        <AuthShell title="Page not found" subtitle="The page you are looking for does not exist.">
            <Link className="btn btn-primary btn-block" to="/">Back to home</Link>
        </AuthShell>
    );
}