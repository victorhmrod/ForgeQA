"use client";

import Link from "next/link";
import { usePathname, useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { projectsApi } from "@/lib/api/projects";

export default function ProjectLayout({ children }: { children: React.ReactNode }) {
  const { projectId } = useParams<{ projectId: string }>();
  const pathname = usePathname();

  const { data: project } = useQuery({
    queryKey: ["projects", "detail", projectId],
    queryFn: () => projectsApi.getById(projectId),
  });

  const basePath = `/app/projects/${projectId}`;
  const tabs = [
    { label: "Overview", href: basePath },
    { label: "Builds", href: `${basePath}/builds` },
  ];

  return (
    <div>
      <header className="border-b border-border px-6 pt-6">
        <p className="font-mono text-xs uppercase tracking-widest text-muted">FORGEQA</p>
        <h1 className="mt-1 text-2xl font-semibold">{project?.name ?? " "}</h1>

        <nav className="mt-5 flex gap-1">
          {tabs.map((tab) => {
            const isActive = tab.href === basePath ? pathname === basePath : pathname.startsWith(tab.href);
            return (
              <Link
                key={tab.href}
                href={tab.href}
                className={`rounded-t-md border-b-2 px-3 py-2 text-sm ${
                  isActive
                    ? "border-accent text-foreground"
                    : "border-transparent text-muted hover:text-foreground"
                }`}
              >
                {tab.label}
              </Link>
            );
          })}
        </nav>
      </header>

      {children}
    </div>
  );
}
