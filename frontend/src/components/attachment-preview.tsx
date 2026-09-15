"use client";

import { useQuery } from "@tanstack/react-query";
import { bugsApi, type BugAttachment } from "@/lib/api/bugs";

export function AttachmentPreview({
  projectId,
  bugId,
  attachment,
}: {
  projectId: string;
  bugId: string;
  attachment: BugAttachment;
}) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["bugs", "attachment-download", projectId, bugId, attachment.id],
    queryFn: () => bugsApi.createAttachmentDownload(projectId, bugId, attachment.id),
    enabled: attachment.status === "READY",
    staleTime: 5 * 60 * 1000,
  });

  if (attachment.status !== "READY") {
    return (
      <div className="flex h-24 w-24 items-center justify-center rounded-md border border-border bg-background text-xs text-muted">
        {attachment.status === "PENDING" ? "Uploading..." : "Failed"}
      </div>
    );
  }

  if (isLoading) {
    return <div className="h-24 w-24 animate-pulse rounded-md border border-border bg-background" />;
  }

  if (isError || !data) {
    return (
      <div className="flex h-24 w-24 items-center justify-center rounded-md border border-border bg-background text-xs text-danger">
        Unavailable
      </div>
    );
  }

  if (attachment.type === "SCREENSHOT") {
    return (
      <a href={data.url} target="_blank" rel="noreferrer">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={data.url}
          alt={attachment.fileName}
          className="h-24 w-24 rounded-md border border-border object-cover transition-opacity hover:opacity-80"
        />
      </a>
    );
  }

  return (
    <a
      href={data.url}
      target="_blank"
      rel="noreferrer"
      className="flex h-24 w-24 items-center justify-center rounded-md border border-border bg-background p-2 text-center text-xs text-accent hover:underline"
    >
      {attachment.fileName}
    </a>
  );
}
