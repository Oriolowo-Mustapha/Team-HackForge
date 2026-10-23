"use client";

import { useQuery } from "@tanstack/react-query";
import { Archive, HeartPulse, Pencil, Plus, Search, Tractor, Users } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useDeferredValue, useState } from "react";
import { livestockApi } from "@/lib/api/endpoints";
import type { Animal, Group } from "@/lib/api/schemas";
import { formatAge, formatNumber, formatRelative, pluralize } from "@/lib/format";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { mediaUrl, smallPhoto, speciesPhoto } from "@/lib/images";
import { qk } from "@/lib/queries";
import { SeverityBadge } from "@/components/domain/badges";
import { GroupFormModal } from "@/components/livestock/group-form";
import { Badge } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Segmented } from "@/components/ui/segmented";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";

type Tab = "groups" | "animals" | "archived";

export function AnimalsScreen() {
  const { farm } = useActiveFarm();
  const farmId = farm?.id ?? "";
  const [tab, setTab] = useState<Tab>("groups");
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());
  const [editing, setEditing] = useState<Group | "new" | null>(null);

  const groups = useQuery({ queryKey: qk.groups(farmId), queryFn: () => livestockApi.groups(farmId), enabled: Boolean(farmId) });
  const animals = useQuery({
    queryKey: qk.animals(farmId, { q, includeInactive: tab === "archived" }),
    queryFn: () => livestockApi.animals(farmId, { q: q || undefined, includeInactive: tab === "archived" }),
    enabled: Boolean(farmId) && tab !== "groups",
    placeholderData: (prev) => prev,
  });

  const groupCount = groups.data?.length ?? 0;
  const animalCount = groups.data?.reduce((s, g) => s + g.registeredAnimals, 0) ?? 0;
  const headcount = groups.data?.reduce((s, g) => s + g.headcount, 0) ?? 0;

  const shown = (animals.data ?? []).filter((a) => (tab === "archived" ? !a.isActive : a.isActive));

  return (
    <div>
      <PageHeader
        title="Animals & flocks"
        subtitle={groups.data ? `${pluralize(groupCount, "group")} · ${formatNumber(headcount)} head · ${pluralize(animalCount, "named animal")}` : undefined}
        actions={
          <div className="hidden gap-2 sm:flex">
            <Button variant="secondary" icon={<Users className="size-4" />} onClick={() => setEditing("new")}>
              Add group
            </Button>
            <ButtonLink href="/animals/new" icon={<Plus className="size-4" />}>
              Add animal
            </ButtonLink>
          </div>
        }
      />

      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Segmented
          label="Show"
          value={tab}
          onChange={setTab}
          options={[
            { value: "groups", label: "Groups", count: groupCount },
            { value: "animals", label: "Named animals", count: animalCount },
            { value: "archived", label: "Archived" },
          ]}
        />
        {tab !== "groups" && (
          <div className="relative sm:w-72">
            <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-faint" aria-hidden />
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search name, breed, tag…" className="pl-11" aria-label="Search animals" type="search" />
          </div>
        )}
      </div>

      <div className="mb-5 grid grid-cols-2 gap-2 sm:hidden">
        <Button variant="secondary" icon={<Users className="size-4" />} onClick={() => setEditing("new")}>
          Add group
        </Button>
        <ButtonLink href="/animals/new" icon={<Plus className="size-4" />}>
          Add animal
        </ButtonLink>
      </div>

      {tab === "groups" ? (
        groups.isPending ? (
          <CardGridSkeleton />
        ) : groups.isError ? (
          <ErrorState error={groups.error} onRetry={() => void groups.refetch()} />
        ) : groups.data.length === 0 ? (
          <EmptyState icon={<Tractor />} title="No groups yet" action={<Button onClick={() => setEditing("new")}>Add your first group</Button>}>
            A group is a flock, herd or pen — like “Layer House A” or “Goats”. Reports about many animals at once are linked to it.
          </EmptyState>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {groups.data.map((g) => (
              <GroupCard key={g.id} group={g} onEdit={() => setEditing(g)} />
            ))}
          </div>
        )
      ) : animals.isPending ? (
        <CardGridSkeleton />
      ) : animals.isError ? (
        <ErrorState error={animals.error} onRetry={() => void animals.refetch()} />
      ) : shown.length === 0 ? (
        <EmptyState
          icon={tab === "archived" ? <Archive /> : <Tractor />}
          title={q ? "No matches" : tab === "archived" ? "Nothing archived" : "No named animals yet"}
          action={tab === "animals" && !q ? <ButtonLink href="/animals/new">Add an animal</ButtonLink> : undefined}
        >
          {q
            ? "Try a different name, breed or tag."
            : tab === "archived"
              ? "Animals you sell, lose or stop tracking can be archived. Their history is kept."
              : "Register animals you track individually — a doe, a bull, a breeding ram — to keep their health history in one place."}
        </EmptyState>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {shown.map((a) => (
            <AnimalCard key={a.id} animal={a} />
          ))}
        </div>
      )}

      {editing && farmId && (
        <GroupFormModal key={editing === "new" ? "new" : editing.id} open onOpenChange={(open) => !open && setEditing(null)} farmId={farmId} group={editing === "new" ? null : editing} />
      )}
    </div>
  );
}

function GroupCard({ group, onEdit }: { group: Group; onEdit: () => void }) {
  return (
    <article className="overflow-hidden rounded-[28px] bg-paper ring-1 ring-line/60">
      <div className="relative h-36">
        <Image src={smallPhoto(speciesPhoto(group.species, group.id))} alt="" fill sizes="(min-width: 1280px) 30vw, (min-width: 640px) 45vw, 100vw" className="object-cover" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink/60 to-transparent" aria-hidden />
        <div className="absolute bottom-3 left-4 right-4 flex items-end justify-between gap-2 text-cream">
          <div className="min-w-0">
            <h2 className="truncate text-lg font-semibold">{group.name}</h2>
            <p className="text-[13px] text-cream/80">{group.species ?? "Mixed"}</p>
          </div>
          <span className="rounded-full bg-cream/90 px-3 py-1 text-sm font-semibold text-ink tabular">{group.headcount > 0 ? `${formatNumber(group.headcount)} head` : `${group.registeredAnimals} named`}</span>
        </div>
      </div>
      <div className="p-4">
        {group.description && <p className="line-clamp-2 text-sm text-muted">{group.description}</p>}
        <p className="mt-1 text-[13px] text-faint">{group.registeredAnimals ? `${pluralize(group.registeredAnimals, "named animal")}` : "No named animals"}</p>
        <div className="mt-4 flex gap-2">
          <ButtonLink href={`/scan?group=${group.id}`} size="sm" variant="accent" icon={<HeartPulse className="size-4" />} className="flex-1">
            Report
          </ButtonLink>
          <Button size="sm" variant="secondary" icon={<Pencil className="size-4" />} onClick={onEdit} aria-label={`Edit ${group.name}`}>
            Edit
          </Button>
        </div>
      </div>
    </article>
  );
}

function AnimalCard({ animal }: { animal: Animal }) {
  const photo = mediaUrl(animal.photoUrl);
  return (
    <Link href={`/animals/${animal.id}`} className="group flex gap-4 rounded-[28px] bg-paper p-3 ring-1 ring-line/60 transition-colors hover:bg-white">
      <span className="relative block size-24 shrink-0 overflow-hidden rounded-[20px] bg-sand">
        {photo ? (
          // eslint-disable-next-line @next/next/no-img-element -- uploaded photos are served through the authenticated BFF
          <img src={photo} alt="" className="size-full object-cover" />
        ) : (
          <Image src={smallPhoto(speciesPhoto(animal.species, animal.id))} alt="" fill sizes="96px" className="object-cover" />
        )}
      </span>
      <span className="min-w-0 flex-1 py-1">
        <span className="block truncate text-[17px] font-semibold">{animal.name}</span>
        <span className="block truncate text-[13px] text-muted">{[animal.species, animal.breed, formatAge(animal.ageMonths), animal.sex].filter(Boolean).join(" · ")}</span>
        <span className="mt-0.5 block truncate text-xs text-faint">{animal.categoryName}</span>
        <span className="mt-2 flex flex-wrap gap-1.5">
          {!animal.isActive && <Badge tone="neutral">Archived</Badge>}
          {animal.highestOpenSeverity !== null ? (
            <SeverityBadge severity={animal.highestOpenSeverity} prefix={animal.openIncidentCount > 1 ? `${animal.openIncidentCount} open ·` : "Open case ·"} />
          ) : (
            <Badge tone="healthy" dot>
              No open cases
            </Badge>
          )}
        </span>
        {animal.lastObservedAt && <span className="mt-1.5 block text-xs text-faint">Last report {formatRelative(animal.lastObservedAt)}</span>}
      </span>
    </Link>
  );
}

function CardGridSkeleton() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" role="status" aria-label="Loading">
      {Array.from({ length: 3 }, (_, i) => (
        <Skeleton key={i} className="h-56 rounded-[28px]" />
      ))}
    </div>
  );
}
