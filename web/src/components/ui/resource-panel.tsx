"use client";

import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Button } from "./button";
import { ListSkeleton } from "./skeleton";
import { ErrorState } from "./states";

export function ResourcePanel<T>({
  query,
  children,
}: {
  query: Pick<
    UseQueryResult<T>,
    "isPending" | "isError" | "error" | "data" | "refetch"
  >;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return <ListSkeleton rows={3} />;
  if (query.isError)
    return (
      <ErrorState error={query.error} onRetry={() => void query.refetch()} />
    );
  if (query.data === undefined) return null;
  return children(query.data);
}

export function Panel({
  title,
  children,
}: {
  title?: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-[28px] bg-paper p-5 ring-1 ring-line/60 sm:p-6">
      {title && <h2 className="mb-4 text-lg font-semibold">{title}</h2>}
      {children}
    </section>
  );
}

export function Pager({
  page,
  totalPages,
  onChange,
}: {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <nav
      aria-label="Pagination"
      className="mt-5 flex items-center justify-between gap-3"
    >
      <Button
        variant="secondary"
        disabled={page <= 1}
        onClick={() => onChange(page - 1)}
      >
        Previous
      </Button>
      <span className="text-sm text-muted">
        Page {page} of {totalPages}
      </span>
      <Button
        variant="secondary"
        disabled={page >= totalPages}
        onClick={() => onChange(page + 1)}
      >
        Next
      </Button>
    </nav>
  );
}
