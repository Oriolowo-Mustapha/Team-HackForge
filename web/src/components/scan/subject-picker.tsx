"use client";

import { useQuery } from "@tanstack/react-query";
import { Check, Users } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { livestockApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";
import { mediaUrl, smallPhoto, speciesPhoto } from "@/lib/images";
import { qk } from "@/lib/queries";
import { ButtonLink } from "@/components/ui/button";
import { ListSkeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";

export type Subject = { kind: "animal" | "group"; id: string; name: string; species: string | null };

/** "Who is this about?" — a flock/herd/pen, or one named animal. */
export function SubjectPicker({ farmId, value, onChange }: { farmId: string; value: Subject | null; onChange: (s: Subject) => void }) {
  const groups = useQuery({ queryKey: qk.groups(farmId), queryFn: () => livestockApi.groups(farmId) });
  const animals = useQuery({ queryKey: qk.animals(farmId, {}), queryFn: () => livestockApi.animals(farmId) });

  if (groups.isPending || animals.isPending) return <ListSkeleton rows={4} />;
  if (groups.isError) return <ErrorState error={groups.error} onRetry={() => void groups.refetch()} />;
  if (animals.isError) return <ErrorState error={animals.error} onRetry={() => void animals.refetch()} />;
  if (groups.data.length === 0)
    return (
      <EmptyState icon={<Users />} title="Add a flock or animal first" action={<ButtonLink href="/animals">Go to Animals</ButtonLink>}>
        Every report is linked to a group or an animal so its history builds up in one place.
      </EmptyState>
    );

  const activeAnimals = (animals.data ?? []).filter((a) => a.isActive);
  const option = (subject: Subject, photo: string, detail: string, uploaded?: string | null) => {
    const selected = value?.id === subject.id;
    return (
      <li key={subject.id}>
        <button
          type="button"
          onClick={() => onChange(subject)}
          aria-pressed={selected}
          className={cn("flex w-full items-center gap-3 rounded-[22px] p-2.5 pr-4 text-left ring-1 transition-colors", selected ? "bg-ink text-cream ring-ink" : "bg-paper ring-line/60 hover:bg-white")}
        >
          <span className="relative block size-14 shrink-0 overflow-hidden rounded-[16px] bg-sand">
            {uploaded ? (
              // eslint-disable-next-line @next/next/no-img-element -- uploaded photo via BFF
              <img src={uploaded} alt="" className="size-full object-cover" />
            ) : (
              <Image src={photo} alt="" fill sizes="56px" className="object-cover" />
            )}
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate font-semibold">{subject.name}</span>
            <span className={cn("block truncate text-[13px]", selected ? "text-cream/70" : "text-muted")}>{detail}</span>
          </span>
          <span className={cn("flex size-6 shrink-0 items-center justify-center rounded-full", selected ? "bg-terracotta text-white" : "ring-1 ring-line")} aria-hidden>
            {selected && <Check className="size-4" />}
          </span>
        </button>
      </li>
    );
  };

  return (
    <div className="space-y-6">
      <section>
        <h3 className="mb-2 text-sm font-semibold text-muted">Flocks, herds & pens</h3>
        <ul className="space-y-2">
          {groups.data.map((g) =>
            option({ kind: "group", id: g.id, name: g.name, species: g.species }, smallPhoto(speciesPhoto(g.species, g.id)), `${g.species ?? "Mixed"} · ${g.headcount > 0 ? `${formatNumber(g.headcount)} head` : `${g.registeredAnimals} named`}`),
          )}
        </ul>
      </section>
      {activeAnimals.length > 0 && (
        <section>
          <h3 className="mb-2 text-sm font-semibold text-muted">Named animals</h3>
          <ul className="grid gap-2 sm:grid-cols-2">
            {activeAnimals.map((a) =>
              option(
                { kind: "animal", id: a.id, name: a.name, species: a.species },
                smallPhoto(speciesPhoto(a.species, a.id)),
                [a.species, a.breed, a.categoryName].filter(Boolean).join(" · "),
                mediaUrl(a.photoUrl),
              ),
            )}
          </ul>
        </section>
      )}
      <p className="text-center text-sm text-muted">
        Not listed?{" "}
        <Link href="/animals/new" className="font-semibold text-ink underline decoration-beige underline-offset-4">
          Add an animal
        </Link>
      </p>
    </div>
  );
}
