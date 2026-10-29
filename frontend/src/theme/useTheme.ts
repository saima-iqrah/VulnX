import { useCallback, useEffect, useState } from 'react';

export type Theme = 'dark' | 'light';
const KEY = 'vulnx.theme';

function current(): Theme {
    return document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
}

export function useTheme() {
    const [theme, setTheme] = useState<Theme>(current);

    useEffect(() => {
        document.documentElement.setAttribute('data-theme', theme);
    }, [theme]);

    const toggle = useCallback(() => {
        setTheme((t) => {
            const next: Theme = t === 'dark' ? 'light' : 'dark';
            try {
                localStorage.setItem(KEY, next);   // remember only when the user chooses
            } catch {
                /* storage may be unavailable */
            }
            return next;
        });
    }, []);

    return { theme, toggle };
}