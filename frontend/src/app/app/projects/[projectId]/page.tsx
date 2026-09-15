"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { projectsApi } from "@/lib/api/projects";
import { buildsApi } from "@/lib/api/builds";
import { bugsApi } from "@/lib/api/bugs";

export default function ProjectDashboardPage() {
  const { projectId } = useParams<{ projectId: string }>();

  const { data: project, isLoading } = useQuery({
    queryKey: ["projects", "detail", projectId],
    queryFn: () => projectsApi.getById(projectId),
  });

  const { data: builds } = useQuery({
    queryKey: ["builds", projectId, { page: 1, pageSize: 1 }],
    queryFn: () => buildsApi.list(projectId, { page: 1, pageSize: 1 }),
  });

  const { data: bugs } = useQuery({
    queryKey: ["bugs", projectId, { page: 1, pageSize: 1 }],
    queryFn: () => bugsApi.list(projectId, { page: 1, pageSize: 1 }),
  });

  if (isLoading) {
    return <main className="px-6 py-8 text-sm text-muted">Loading...</main>;
  }

  if (!project) {
    return <main className="px-6 py-8 text-sm text-danger">Project not found.</main>;
  }

  return (
    <main className="mx-auto max-w-4xl px-6 py-8">
      {project.description && <p className="-mt-4 mb-6 text-sm text-muted">{project.description}</p>}

      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Overview</h2>

      <div className="grid gap-4 sm:grid-cols-3">
        <Link href={`/app/projects/${projectId}/builds`}>
          <DashboardSection
            title="Builds"
            emptyLabel="No builds yet."
            count={builds?.totalCount}
          />
        </Link>
        <Link href={`/app/projects/${projectId}/bugs`}>
          <DashboardSection title="Bugs" emptyLabel="No bugs reported yet." count={bugs?.totalCount} />
        </Link>
        <DashboardSection title="Sessions" emptyLabel="No sessions yet." />
      </div>
    </main>
  );
}

function DashboardSection({ title, emptyLabel, count }: { title: string; emptyLabel: string; count?: number }) {
  return (
    <section className="rounded-lg border border-border bg-surface p-4 transition-colors hover:bg-surface-hover">
      <h3 className="text-sm font-medium">{title}</h3>
      <p className="mt-2 text-sm text-muted">
        {count === undefined ? emptyLabel : count === 0 ? emptyLabel : `${count} registered`}
      </p>
    </section>
  );
}
