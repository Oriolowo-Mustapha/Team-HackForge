"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { livestockApi, type AnimalInput } from "@/lib/api/endpoints";
import type { Animal } from "@/lib/api/schemas";
import { qk } from "@/lib/queries";
import { Button } from "@/components/ui/button";
import { ChoiceChips, Field, Input, Select, Textarea } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";
import { SEX_OPTIONS, SPECIES } from "./species";

const dateOnly = (value: string | null | undefined) => (value ? value.slice(0, 10) : "");

/** Create or edit an individually tracked animal. */
export function AnimalForm({ farmId, animal, defaultGroupId }: { farmId: string; animal?: Animal; defaultGroupId?: string | null }) {
  const router = useRouter();
  const client = useQueryClient();
  const groups = useQuery({ queryKey: qk.groups(farmId), queryFn: () => livestockApi.groups(farmId) });
  const [showHistory, setShowHistory] = useState(Boolean(animal && (animal.vaccinations || animal.diseaseHistory || animal.lastVeterinaryVisit)));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [form, setForm] = useState({
    categoryId: animal?.categoryId ?? defaultGroupId ?? "",
    name: animal?.name ?? "",
    species: animal?.species ?? "",
    breed: animal?.breed ?? "",
    ageYears: animal?.ageMonths != null ? String(Math.floor(animal.ageMonths / 12)) : "",
    ageMonths: animal?.ageMonths != null ? String(animal.ageMonths % 12) : "",
    sex: animal?.sex ?? "",
    notes: animal?.notes ?? "",
    vaccinations: animal?.vaccinations ?? "",
    diseaseHistory: animal?.diseaseHistory ?? "",
    lastVeterinaryVisit: dateOnly(animal?.lastVeterinaryVisit),
    recentGestationDate: dateOnly(animal?.recentGestationDate),
    lastLactationDate: dateOnly(animal?.lastLactationDate),
    parentage: animal?.parentage ?? "",
  });
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [key]: e.target.value }));

  const save = useMutation({
    mutationFn: (input: AnimalInput) => (animal ? livestockApi.updateAnimal(animal.id, input) : livestockApi.createAnimal(farmId, input)),
    onSuccess: async (saved) => {
      toast.success(animal ? "Changes saved" : `${saved.name} added`);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["animals", farmId] }),
        client.invalidateQueries({ queryKey: qk.animal(saved.id) }),
        client.invalidateQueries({ queryKey: qk.groups(farmId) }),
        client.invalidateQueries({ queryKey: qk.overview(farmId) }),
      ]);
      router.replace(`/animals/${saved.id}`);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (!form.categoryId) found.categoryId = "Choose the group this animal belongs to.";
    if (!form.name.trim()) found.name = "Enter a name or tag number.";
    if (!form.species) found.species = "Choose a species.";
    const years = form.ageYears ? Number(form.ageYears) : 0;
    const months = form.ageMonths ? Number(form.ageMonths) : 0;
    if (months > 11) found.age = "Months should be 0–11; add whole years in the first box.";
    setErrors(found);
    if (Object.keys(found).length) return;
    const ageMonths = form.ageYears || form.ageMonths ? years * 12 + months : null;
    const orNull = (v: string) => (v.trim() ? v.trim() : null);
    save.mutate({
      categoryId: form.categoryId,
      name: form.name.trim(),
      species: form.species,
      breed: orNull(form.breed),
      ageMonths,
      sex: orNull(form.sex),
      notes: orNull(form.notes),
      vaccinations: orNull(form.vaccinations),
      diseaseHistory: orNull(form.diseaseHistory),
      lastVeterinaryVisit: orNull(form.lastVeterinaryVisit),
      recentGestationDate: orNull(form.recentGestationDate),
      lastLactationDate: orNull(form.lastLactationDate),
      parentage: orNull(form.parentage),
    });
  }

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      <Field label="Group" hint="The flock, herd or pen this animal lives in." error={errors.categoryId}>
        <Select
          value={form.categoryId}
          onChange={(e) => {
            const group = groups.data?.find((g) => g.id === e.target.value);
            setForm((f) => ({ ...f, categoryId: e.target.value, species: f.species || group?.species || "" }));
          }}
          disabled={groups.isPending}
        >
          <option value="">{groups.isPending ? "Loading groups…" : "Choose a group…"}</option>
          {groups.data?.map((g) => (
            <option key={g.id} value={g.id}>
              {g.name}
              {g.species ? ` · ${g.species}` : ""}
            </option>
          ))}
        </Select>
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name or tag" error={errors.name}>
          <Input value={form.name} onChange={set("name")} placeholder="Adunni" />
        </Field>
        <Field label="Species" error={errors.species}>
          <Select value={form.species} onChange={set("species")}>
            <option value="">Choose…</option>
            {SPECIES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
            {form.species && !SPECIES.includes(form.species as (typeof SPECIES)[number]) && <option value={form.species}>{form.species}</option>}
          </Select>
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Breed" optional>
          <Input value={form.breed} onChange={set("breed")} placeholder="West African Dwarf" />
        </Field>
        <Field label="Age" optional error={errors.age}>
          <div className="flex gap-2">
            <div className="relative flex-1">
              <Input inputMode="numeric" aria-label="Years" value={form.ageYears} onChange={(e) => setForm((f) => ({ ...f, ageYears: e.target.value.replace(/[^\d]/g, "").slice(0, 2) }))} placeholder="2" className="pr-14" />
              <span className="pointer-events-none absolute right-4 top-1/2 -translate-y-1/2 text-sm text-faint">yrs</span>
            </div>
            <div className="relative flex-1">
              <Input inputMode="numeric" aria-label="Months" value={form.ageMonths} onChange={(e) => setForm((f) => ({ ...f, ageMonths: e.target.value.replace(/[^\d]/g, "").slice(0, 2) }))} placeholder="2" className="pr-14" />
              <span className="pointer-events-none absolute right-4 top-1/2 -translate-y-1/2 text-sm text-faint">mo</span>
            </div>
          </div>
        </Field>
      </div>
      <div className="space-y-2">
        <p className="text-sm font-semibold">Sex</p>
        <ChoiceChips label="Sex" options={SEX_OPTIONS} value={form.sex || null} onChange={(v) => setForm((f) => ({ ...f, sex: v }))} />
      </div>
      <Field label="Notes" optional>
        <Textarea value={form.notes} onChange={set("notes")} placeholder="Temperament, markings, ear tag…" className="min-h-20" />
      </Field>

      <div className="rounded-[24px] bg-paper ring-1 ring-line/60">
        <button type="button" onClick={() => setShowHistory((v) => !v)} aria-expanded={showHistory} className="flex w-full items-center justify-between gap-3 px-5 py-4 text-left">
          <span>
            <span className="block font-semibold">Health history</span>
            <span className="text-[13px] text-muted">Optional — helps the triage and the vet.</span>
          </span>
          <ChevronDown className={`size-5 transition-transform ${showHistory ? "rotate-180" : ""}`} aria-hidden />
        </button>
        {showHistory && (
          <div className="space-y-4 border-t border-line px-5 pb-5 pt-4">
            <Field label="Vaccinations" optional>
              <Input value={form.vaccinations} onChange={set("vaccinations")} placeholder="PPR (March), CDT booster (May)" />
            </Field>
            <Field label="Past illnesses or treatments" optional>
              <Textarea value={form.diseaseHistory} onChange={set("diseaseHistory")} className="min-h-20" />
            </Field>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Last vet visit" optional>
                <Input type="date" value={form.lastVeterinaryVisit} onChange={set("lastVeterinaryVisit")} />
              </Field>
              <Field label="Last mated / gestation" optional>
                <Input type="date" value={form.recentGestationDate} onChange={set("recentGestationDate")} />
              </Field>
              <Field label="Last lactation" optional>
                <Input type="date" value={form.lastLactationDate} onChange={set("lastLactationDate")} />
              </Field>
            </div>
            <Field label="Parentage" optional>
              <Input value={form.parentage} onChange={set("parentage")} placeholder="Dam: Adunni · Sire: Kunle" />
            </Field>
          </div>
        )}
      </div>

      <InlineError error={save.error} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button variant="secondary" size="lg" onClick={() => router.back()}>
          Cancel
        </Button>
        <Button type="submit" size="lg" loading={save.isPending}>
          {animal ? "Save changes" : "Add animal"}
        </Button>
      </div>
    </form>
  );
}
