export function TrendChart({ points }: { points: { date: string; score: number }[] }) {
    if (points.length < 2) {
        return <p className="muted">Complete at least two scans to see your score trend.</p>;
    }
    const W = 600, H = 190, P = 30;
    const xs = points.map((_, i) => P + (i * (W - 2 * P)) / (points.length - 1));
    const ys = points.map((p) => H - P - (Math.max(0, Math.min(100, p.score)) / 100) * (H - 2 * P));
    const path = xs.map((x, i) => `${i === 0 ? 'M' : 'L'}${x},${ys[i]}`).join(' ');

    return (
        <svg viewBox={`0 0 ${W} ${H}`} className="trend" role="img" aria-label="Security score trend">
            {[0, 50, 100].map((v) => {
                const y = H - P - (v / 100) * (H - 2 * P);
                return (
                    <g key={v}>
                        <line x1={P} x2={W - P} y1={y} y2={y} style={{ stroke: 'var(--line)' }} strokeDasharray="4 4" />
                        <text x={4} y={y + 4} fontSize="11" style={{ fill: 'var(--muted)' }}>{v}</text>
                    </g>
                );
            })}
            <path d={path} fill="none" style={{ stroke: 'var(--brand)' }} strokeWidth="2.5" />
            {points.map((p, i) => (
                <g key={i}>
                    <circle cx={xs[i]} cy={ys[i]} r="4" style={{ fill: 'var(--brand2)' }} />
                    <text x={xs[i]} y={ys[i] - 9} textAnchor="middle" fontSize="11" style={{ fill: 'var(--text)' }}>{p.score}</text>
                    <text x={xs[i]} y={H - 8} textAnchor="middle" fontSize="10" style={{ fill: 'var(--muted)' }}>
                        {new Date(p.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                    </text>
                </g>
            ))}
        </svg>
    );
}