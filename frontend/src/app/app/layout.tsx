"use client";

import Link from "next/link";
import { useAuth } from "@/lib/auth/auth-context";
import { AuthGuard } from "@/components/auth-guard";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <div className="flex flex-1">
        <Sidebar />
        <div className="flex-1 overflow-y-auto">{children}</div>
      </div>
    </AuthGuard>
  );
}

function Sidebar() {
  const { user, logout } = useAuth();

  return (
    <aside className="flex w-56 shrink-0 flex-col border-r border-border bg-surface">
      <div className="border-b border-border px-4 py-4">
        <Link href="/app" className="font-mono text-sm font-semibold tracking-widest">
          FORGEQA
        </Link>
      </div>

      <nav className="flex-1 px-2 py-4 text-sm">
        <Link
          href="/app"
          className="block rounded-md px-3 py-2 text-foreground hover:bg-surface-hover"
        >
          Projects
        </Link>
      </nav>

      <div className="border-t border-border px-4 py-3 text-xs text-muted">
        <p className="truncate">{user?.displayName}</p>
        <p className="truncate text-muted">{user?.email}</p>
        <button onClick={() => logout()} className="mt-2 text-accent hover:underline">
          Log out
        </button>
      </div>
    </aside>
  );
}
