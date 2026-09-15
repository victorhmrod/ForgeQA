import { Badge } from "@/components/badge";
import { SEVERITY_LABELS, STATUS_LABELS, type BugSeverity, type BugStatusValue } from "@/lib/api/bugs";

export function SeverityBadge({ severity }: { severity: BugSeverity }) {
  const tone = severity === "CRITICAL" || severity === "HIGH" ? "danger" : severity === "MEDIUM" ? "warning" : "muted";
  return <Badge tone={tone}>{SEVERITY_LABELS[severity]}</Badge>;
}

export function StatusBadge({ status }: { status: BugStatusValue }) {
  const tone = status === "OPEN" ? "warning" : status === "IN_PROGRESS" ? "neutral" : status === "RESOLVED" ? "success" : "muted";
  return <Badge tone={tone}>{STATUS_LABELS[status]}</Badge>;
}
