"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, ArchiveRestore, Camera, HeartPulse, Pencil, Syringe } from "lucide-react";
import Image from "next/image";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { livestockApi } from "@/lib/api/endpoints";
import type { Animal } from "@/lib/api/schemas";
import { formatAge, formatDate } from "@/lib/format";
import { ACCEPTED_IMAGE_TYPES, compressImage } from "@/lib/image-tools";
import { mediaUrl, speciesPhoto } from "@/lib/images";
import { qk } from "@/lib/queries";
import { IncidentMiniRow, ObservationRow } from "@/components/domain/rows";
import { Badge } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/dialog";
import { PageHeader, SectionHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Spinner } from "@/components/ui/spinner";

export function AnimalDetail({ id }: { id: string }) {
  const detail = useQuery({ queryKey: qk.animal(id), queryFn: () => livestockApi.animal(id) });

  if (detail.isPending)
    return (
      <div className="space-y-4">
        <Skeleton className="h-72 rounded-[32px]" />
        <Skeleton className="h-40 rounded-[28px]" />
      </div>
    );
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;

  const { animal, observations, incidents } = detail.data;
  const open = incidents.filter((i) => i.status !== 4);
  const closed = incidents.filter((i) => i.status === 4);

  return (
    <div>
      <PageHeader back="/animals" title={animal.name} eyebrow={animal.categoryName ?? undefined} className="lg:hidden" />
      <div className="grid gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-5">
          <Hero animal={animal} />
          <HealthProfile animal={animal} />
        </div>
        <div className="space-y-6 lg:col-span-7">
          <section>
            <SectionHeader title="Open cases" count={open.length} />
            {open.length ? (
              <div className="space-y-2.5">
                {open.map((i) => (
                  <IncidentMiniRow key={i.id} incident={i} />
                ))}
              </div>
            ) : (
              <p className="rounded-[24px] bg-paper px-5 py-4 text-sm text-muted ring-1 ring-line/60">No open cases for {animal.name}.</p>
            )}
          </section>
          <section>
            <SectionHeader title="Health reports" count={observations.filter((o) => o.confirmationStatus === 0).length} />
            {observations.length ? (
              <div className="space-y-2.5">
                {observations.map((o) => (
                  <ObservationRow key={o.id} item={o} />
                ))}
              </div>
            ) : (
              <EmptyState icon={<HeartPulse />} title="No reports yet" action={<ButtonLink href={`/scan?animal=${animal.id}`} variant="accent">Report on {animal.name}</ButtonLink>}>
                Anything you notice — appetite, behaviour, a swelling — becomes part of {animal.name}&apos;s history.
              </EmptyState>
            )}
          </section>
          {closed.length > 0 && (
            <section>
              <SectionHeader title="Past cases" />
              <div className="space-y-2.5">
                {closed.map((i) => (
                  <IncidentMiniRow key={i.id} incident={i} />
                ))}
              </div>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}

function Hero({ animal }: { animal: Animal }) {
  const client = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const [confirm, setConfirm] = useState(false);
  const photo = mediaUrl(animal.photoUrl);

  const invalidate = () =>
    Promise.all([client.invalidateQueries({ queryKey: qk.animal(animal.id) }), client.invalidateQueries({ queryKey: ["animals", animal.farmId] }), client.invalidateQueries({ queryKey: qk.groups(animal.farmId) })]);

  const upload = useMutation({
    mutationFn: async (file: File) => livestockApi.uploadPhoto(animal.id, await compressImage(file, 1200)),
    onSuccess: async () => {
      toast.success("Photo updated");
      await invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : "The photo could not be uploaded."),
  });

  const archive = useMutation({
    mutationFn: () => (animal.isActive ? livestockApi.archive(animal.id) : livestockApi.restore(animal.id)),
    onSuccess: async (a) => {
      toast.success(a.isActive ? `${a.name} restored` : `${a.name} archived`);
      setConfirm(false);
      await invalidate();
    },
  });

  return (
    <div className="overflow-hidden rounded-[32px] bg-paper ring-1 ring-line/60">
      <div className="relative h-64 sm:h-72">
        {photo ? (
          // eslint-disable-next-line @next/next/no-img-element -- uploaded photos are served through the authenticated BFF
          <img src={photo} alt={`Photo of ${animal.name}`} className="absolute inset-0 size-full object-cover" />
        ) : (
          <Image src={`${speciesPhoto(animal.species, animal.id)}.webp`} alt="" fill priority sizes="(min-width: 1024px) 40vw, 100vw" className="object-cover" />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-ink/70 via-transparent to-transparent" aria-hidden />
        {!photo && <span className="absolute left-4 top-4 rounded-full bg-ink/60 px-3 py-1 text-xs font-semibold text-cream backdrop-blur">Illustrative photo</span>}
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={upload.isPending}
          className="absolute right-4 top-4 inline-flex h-10 items-center gap-2 rounded-full bg-paper/95 px-4 text-sm font-semibold text-ink shadow-soft hover:bg-white disabled:opacity-60"
        >
          {upload.isPending ? <Spinner className="size-4" /> : <Camera className="size-4" aria-hidden />}
          {photo ? "Change photo" : "Add photo"}
        </button>
        <input
          ref={fileRef}
          type="file"
          accept={ACCEPTED_IMAGE_TYPES}
          className="sr-only"
          tabIndex={-1}
          aria-hidden
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) upload.mutate(file);
            e.target.value = "";
          }}
        />
        <div className="absolute bottom-4 left-5 right-5 text-cream">
          <h1 className="text-[30px] font-semibold leading-tight tracking-[-0.03em]">{animal.name}</h1>
          <p className="text-sm text-cream/85">{[animal.species, animal.breed, formatAge(animal.ageMonths), animal.sex].filter(Boolean).join(" · ")}</p>
        </div>
      </div>
      <div className="space-y-3 p-4">
        {!animal.isActive && <Badge tone="neutral">Archived — history kept</Badge>}
        <div className="grid grid-cols-2 gap-2">
          {animal.isActive && (
            <ButtonLink href={`/scan?animal=${animal.id}`} variant="accent" icon={<HeartPulse className="size-4" />} className="col-span-2">
              Report a concern
            </ButtonLink>
          )}
          <ButtonLink href={`/animals/${animal.id}/edit`} variant="secondary" icon={<Pencil className="size-4" />}>
            Edit
          </ButtonLink>
          <Button variant="secondary" icon={animal.isActive ? <Archive className="size-4" /> : <ArchiveRestore className="size-4" />} onClick={() => setConfirm(true)}>
            {animal.isActive ? "Archive" : "Restore"}
          </Button>
        </div>
      </div>
      <ConfirmDialog
        open={confirm}
        onOpenChange={setConfirm}
        title={animal.isActive ? `Archive ${animal.name}?` : `Restore ${animal.name}?`}
        description={
          animal.isActive
            ? "Use this when an animal is sold, has died or you stop tracking it. Reports and cases stay in the record, and you can restore it later."
            : `${animal.name} will appear in your animal list and can be reported on again.`
        }
        confirmLabel={animal.isActive ? "Archive" : "Restore"}
        tone={animal.isActive ? "danger" : "primary"}
        loading={archive.isPending}
        onConfirm={() => archive.mutate()}
      />
    </div>
  );
}

function HealthProfile({ animal }: { animal: Animal }) {
  const rows = [
    { label: "Vaccinations", value: animal.vaccinations },
    { label: "Past illnesses", value: animal.diseaseHistory },
    { label: "Last vet visit", value: animal.lastVeterinaryVisit ? formatDate(animal.lastVeterinaryVisit) : null },
    { label: "Last mated / gestation", value: animal.recentGestationDate ? formatDate(animal.recentGestationDate) : null },
    { label: "Last lactation", value: animal.lastLactationDate ? formatDate(animal.lastLactationDate) : null },
    { label: "Parentage", value: animal.parentage },
    { label: "Notes", value: animal.notes },
  ];
  const filled = rows.filter((r) => r.value);
  return (
    <Card>
      <CardTitle action={<Syringe className="size-4 text-faint" aria-hidden />}>Health profile</CardTitle>
      {filled.length ? (
        <dl className="divide-y divide-line/70">
          {filled.map((r) => (
            <div key={r.label} className="grid grid-cols-[minmax(0,2fr)_minmax(0,3fr)] gap-3 py-2.5 text-sm">
              <dt className="text-muted">{r.label}</dt>
              <dd className="font-medium">{r.value}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className="text-sm text-muted">No health history recorded. Adding vaccinations and past illnesses helps a vet review cases faster.</p>
      )}
      <p className="mt-3 text-xs text-faint">Added {formatDate(animal.createdAt)}</p>
    </Card>
  );
}
