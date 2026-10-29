import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { notificationsApi } from '../api/platform';

export function NotificationBell() {
    const [count, setCount] = useState(0);

    useEffect(() => {
        let on = true;
        const load = () =>
            notificationsApi.unreadCount().then((r) => { if (on) setCount(r.count); }).catch(() => undefined);
        load();
        const timer = setInterval(load, 30000);
        return () => { on = false; clearInterval(timer); };
    }, []);

    return (
        <Link to="/app/notifications" className="bell" aria-label={`Notifications, ${count} unread`}>
            <span aria-hidden="true">&#128276;</span>
            {count > 0 && <span className="bell-count">{count > 99 ? '99+' : count}</span>}
        </Link>
    );
}