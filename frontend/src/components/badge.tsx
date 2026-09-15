export function Badge({ tone = "neutral", children }: { tone?: "neutral" | "success" | "muted"; children: React.ReactNode }) {
  const toneClasses = {
    neutral: "border-border bg-surface text-foreground",
    success: "border-success/30 bg-success/10 text-success",
    muted: "border-border bg-transparent text-muted",
  }[tone];

  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${toneClasses}`}>
      {children}
    </span>
  );
}
