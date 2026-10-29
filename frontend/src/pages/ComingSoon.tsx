import { Card, PageHeader } from '../components/ui';

export default function ComingSoon({ title, phase }: { title: string; phase: string }) {
    return (
        <>
            <PageHeader title={title} />
            <Card>
                <p className="muted">This section is planned for {phase}. The navigation is in place so the dashboard layout stays stable.</p>
            </Card>
        </>
    );
}