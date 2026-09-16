"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { telemetryApi } from "@/lib/api/telemetry";
import { Badge } from "@/components/badge";

const EVENTS_PAGE_SIZE = 100;

export default function TelemetrySessionDetailPage() {
  const { projectId, sessionId } = useParams<{ projectId: string; sessionId: string }>();
  const [eventsPage, setEventsPage] = useState(1);

  const { data: session, isLoading, isError } = useQuery({
    queryKey: ["telemetry-sessions", "detail", projectId, sessionId],
    queryFn: () => telemetryApi.getById(projectId, sessionId),
  });

  const { data: events, isLoading: eventsLoading } = useQuery({
    queryKey: ["telemetry-sessions", "events", projectId, sessionId, eventsPage],
    queryFn: () => telemetryApi.getEvents(projectId, sessionId, { page: eventsPage, pageSize: EVENTS_PAGE_SIZE }),
  });

  if (isLoading) {
    return <main className="px-6 py-8 text-sm text-muted">Loading...</main>;
  }

  if (isError || !session) {
    return <main className="px-6 py-8 text-sm text-danger">Telemetry session not found.</main>;
  }

  return (
    <main className="mx-auto max-w-4xl px-6 py-8">
      <Link href={`/app/projects/${projectId}/telemetry`} className="text-sm text-accent hover:underline">
        &larr; Back to telemetry
      </Link>

      <div className="mt-4 flex items-start justify-between gap-4">
        <div>
          <h1 className="font-mono text-lg font-semibold">{session.runtimeSessionId}</h1>
          <p className="mt-1 text-xs text-muted">Started {new Date(session.startedAt).toLocaleString()}</p>
        </div>
        <Badge tone={session.endedAt ? "muted" : "success"}>{session.endedAt ? "Ended" : "Active"}</Badge>
      </div>

      <div className="mt-6 flex flex-col gap-6">
        <Section title="Session">
          <Field label="Started at" value={new Date(session.startedAt).toLocaleString()} />
          <Field label="Ended at" value={session.endedAt ? new Date(session.endedAt).toLocaleString() : "Still active"} />
          <Field label="Last event at" value={session.lastEventAt ? new Date(session.lastEventAt).toLocaleString() : "No events yet"} />
          <Field label="Event count" value={String(session.eventCount)} />
        </Section>

        <Section title="Build">
          <Field label="Version" value={`${session.build.version} · ${session.build.buildNumber}`} />
          <Field label="Platform / Configuration" value={`${session.build.platform} / ${session.build.configuration}`} />
          <div className="sm:col-span-2">
            <Link href={`/app/projects/${projectId}/builds/${session.build.id}`} className="text-sm text-accent hover:underline">
              View build detail &rarr;
            </Link>
          </div>
        </Section>

        {hasEnvironmentData(session.environment) && (
          <Section title="Environment">
            {session.environment.mapName && <Field label="Map" value={session.environment.mapName} />}
            {session.environment.gameMode && <Field label="Game mode" value={session.environment.gameMode} />}
            {session.environment.platform && <Field label="Platform" value={session.environment.platform} />}
            {session.environment.configuration && <Field label="Configuration" value={session.environment.configuration} />}
            {session.environment.engineVersion && <Field label="Engine version" value={session.environment.engineVersion} />}
            {session.environment.osVersion && <Field label="OS" value={session.environment.osVersion} />}
            {session.environment.locale && <Field label="Locale" value={session.environment.locale} />}
          </Section>
        )}

        <section className="rounded-lg border border-border bg-surface p-4">
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Events</h2>

          {eventsLoading && <p className="text-sm text-muted">Loading events...</p>}

          {!eventsLoading && events?.items.length === 0 && <p className="text-sm text-muted">No events recorded yet.</p>}

          {!eventsLoading && events && events.items.length > 0 && (
            <>
              <ol className="flex flex-col divide-y divide-border font-mono text-xs">
                {events.items.map((event) => (
                  <li key={event.id} className="flex flex-col gap-1 py-2">
                    <div className="flex items-center gap-3">
                      <span className="text-muted">#{event.sequenceNumber}</span>
                      <span className="text-muted">{new Date(event.clientTimestamp).toLocaleTimeString()}</span>
                      <span className="font-semibold text-foreground">{event.eventName}</span>
                      {event.mapName && <span className="text-muted">({event.mapName})</span>}
                    </div>
                    {Object.keys(event.properties).length > 0 && (
                      <pre className="whitespace-pre-wrap break-all rounded-md bg-background px-3 py-2 text-[11px] text-muted">
                        {JSON.stringify(event.properties, null, 2)}
                      </pre>
                    )}
                  </li>
                ))}
              </ol>

              <div className="mt-4 flex items-center justify-between text-sm text-muted">
                <span>
                  Page {events.page} of {Math.max(events.totalPages, 1)} &middot; {events.totalCount} event
                  {events.totalCount === 1 ? "" : "s"}
                </span>
                <div className="flex gap-2">
                  <button
                    onClick={() => setEventsPage((p) => Math.max(1, p - 1))}
                    disabled={eventsPage <= 1}
                    className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40"
                  >
                    Previous
                  </button>
                  <button
                    onClick={() => setEventsPage((p) => p + 1)}
                    disabled={eventsPage >= events.totalPages}
                    className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40"
                  >
                    Load more
                  </button>
                </div>
              </div>
            </>
          )}
        </section>
      </div>
    </main>
  );
}

function hasEnvironmentData(environment: {
  mapName: string | null;
  gameMode: string | null;
  platform: string | null;
  configuration: string | null;
  engineVersion: string | null;
  osVersion: string | null;
  locale: string | null;
}) {
  return Object.values(environment).some((value) => value !== null && value !== "");
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-border bg-surface p-4">
      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">{title}</h2>
      <div className="grid gap-3 sm:grid-cols-2">{children}</div>
    </section>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-muted">{label}</p>
      <p className="mt-0.5 text-sm">{value}</p>
    </div>
  );
}
