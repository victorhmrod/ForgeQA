"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationsApi } from "@/lib/api/organizations";
import { projectsApi } from "@/lib/api/projects";
import { ApiError } from "@/lib/api/client";
import { FormField } from "@/components/form-field";

export default function ProjectsPage() {
  const { data: organizations } = useQuery({
    queryKey: ["organizations"],
    queryFn: organizationsApi.list,
  });

  const organization = organizations?.[0];

  const { data: projects, isLoading: isLoadingProjects } = useQuery({
    queryKey: ["projects", organization?.id],
    queryFn: () => projectsApi.listForOrganization(organization!.id),
    enabled: !!organization,
  });

  const [isCreating, setIsCreating] = useState(false);

  return (
    <main className="mx-auto max-w-4xl px-6 py-8">
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold">Projects</h1>
          {organization && <p className="text-sm text-muted">{organization.name}</p>}
        </div>
        <button
          onClick={() => setIsCreating(true)}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground"
        >
          Create Project
        </button>
      </header>

      {isCreating && organization && (
        <CreateProjectForm organizationId={organization.id} onClose={() => setIsCreating(false)} />
      )}

      {isLoadingProjects && <p className="text-sm text-muted">Loading projects...</p>}

      {!isLoadingProjects && projects?.length === 0 && (
        <p className="text-sm text-muted">No projects yet. Create one to get started.</p>
      )}

      <ul className="flex flex-col gap-2">
        {projects?.map((project) => (
          <li key={project.id}>
            <Link
              href={`/app/projects/${project.id}`}
              className="block rounded-md border border-border bg-surface px-4 py-3 hover:bg-surface-hover"
            >
              <p className="font-medium">{project.name}</p>
              <p className="font-mono text-xs text-muted">{project.slug}</p>
            </Link>
          </li>
        ))}
      </ul>
    </main>
  );
}

function CreateProjectForm({ organizationId, onClose }: { organizationId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => projectsApi.create(organizationId, { name, description: description || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects", organizationId] });
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail ?? err.message : "Something went wrong."),
  });

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        mutation.mutate();
      }}
      className="mb-6 flex flex-col gap-4 rounded-lg border border-border bg-surface p-4"
    >
      <FormField label="Name" required value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      <FormField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />

      {error && <p className="text-sm text-danger">{error}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={mutation.isPending}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
        >
          {mutation.isPending ? "Creating..." : "Create"}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md border border-border px-3 py-2 text-sm text-muted hover:bg-surface-hover"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
