import { useEffect, useLayoutEffect, useRef } from 'react';
import type { RefObject } from 'react';

/**
 * Stops the browser from inserting a saved login into the form when the page opens.
 * The inputs stay read-only (browsers do not auto-fill those) until the person clicks, touches the screen or
 * presses a key. After that the fields are normal again and the browser's own dropdown with saved emails and
 * passwords works as usual. Anything inserted before that first interaction is emptied.
 */
export function useNoPrefill(form: RefObject<HTMLFormElement | null>, reset: () => void) {
    const touched = useRef(false);

    useLayoutEffect(() => {
        const el = form.current;
        if (!el) return;

        const inputs = () => Array.from(el.querySelectorAll('input'));
        if (!touched.current) inputs().forEach((i) => { i.readOnly = true; });

        const unlock = () => {
            touched.current = true;
            inputs().forEach((i) => { i.readOnly = false; });
        };
        window.addEventListener('pointerdown', unlock, true);
        window.addEventListener('touchstart', unlock, { capture: true, passive: true });
        window.addEventListener('keydown', unlock, true);

        return () => {
            window.removeEventListener('pointerdown', unlock, true);
            window.removeEventListener('touchstart', unlock, true);
            window.removeEventListener('keydown', unlock, true);
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // Backup: if something was still inserted before the first interaction, empty it.
    useEffect(() => {
        const el = form.current;
        if (!el) return;
        const clear = () => {
            if (touched.current) return;
            el.querySelectorAll('input').forEach((i) => { i.value = ''; });
            reset();
        };
        const timers = [50, 300, 800, 1500, 3000].map((ms) => window.setTimeout(clear, ms));
        return () => timers.forEach((t) => window.clearTimeout(t));
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
}