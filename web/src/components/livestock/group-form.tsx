"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { livestockApi, type GroupInput } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import type { Group } from "@/lib/api/schemas";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";
import { toast } from "sonner";
import { SPECIES } from "./species";

/** Create or edit a flock, herd or pen. */
export function GroupFormModal({
  open,
  onOpenChange,
  farmId,
  group,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  farmId: string;
  group?: Group | null;
  onSaved?: (id: string) => void;
}) {
  const client = useQueryClient();
  const [form, setForm] = useState({
    name: group?.name ?? "",
    species: group?.species ?? "",
    headcount: group?.headcount ? String(group.headcount) : "",
    description: group?.description ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const save = useMutation({
    mutationFn: async (input: GroupInput) => {
      if (group) return (await livestockApi.updateGroup(group.id, input)).id;
      return (await livestockApi.createGroup(farmId, input)).id;
    },
    onSuccess: async (id) => {
      toast.success(group ? "Group updated" : "Group added");
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.groups(farmId) }),
        client.invalidateQueries({ queryKey: qk.overview(farmId) }),
      ]);
      onOpenChange(false);
      onSaved?.(id);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (form.name.trim().length < 2) found.name = "Enter a name, e.g. “Layer House A”.";
    const count = form.headcount ? Number(form.headcount) : null;
    if (count !== null && (!Number.isInteger(count) || count < 0 || count > 1_000_000)) found.headcount = "Enter a whole number.";
    setErrors(found);
    if (Object.keys(found).length) return;
    save.mutate({ name: form.name.trim(), species: form.species || null, headcount: count, description: form.description.trim() || null });
  }

  return (
    <Modal open={open} onOpenChange={onOpenChange} title={group ? `Edit ${group.name}` : "Add a flock, herd or pen"} description="Reports about the whole group are linked here.">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Name" error={errors.name}>
          <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="Layer House A" />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Species">
            <Select value={form.species} onChange={(e) => setForm((f) => ({ ...f, species: e.target.value }))}>
              <option value="">Choose…</option>
              {SPECIES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
              {form.species && !SPECIES.includes(form.species as (typeof SPECIES)[number]) && <option value={form.species}>{form.species}</option>}
            </Select>
          </Field>
          <Field label="Headcount" optional error={errors.headcount}>
            <Input inputMode="numeric" value={form.headcount} onChange={(e) => setForm((f) => ({ ...f, headcount: e.target.value.replace(/[^\d]/g, "") }))} placeholder="480" />
          </Field>
        </div>
        <Field label="Notes" optional>
          <Textarea value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} placeholder="Breed, age, housing…" className="min-h-20" />
        </Field>
        <InlineError error={save.error} />
        <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" loading={save.isPending}>
            {group ? "Save changes" : "Add group"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
