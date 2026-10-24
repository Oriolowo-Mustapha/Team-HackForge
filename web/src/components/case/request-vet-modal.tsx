"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { vetsApi } from "@/lib/api/endpoints";
import { requestTypeMeta } from "@/lib/api/enums";
import type { IncidentCase } from "@/lib/api/schemas";
import { qk } from "@/lib/queries";
import { formatMoney } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { ChoiceChips, Field, Select, Textarea, Input } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";

/** Platform pricing: one fixed consultation fee, FarmHand adds its 5% service on top at payment time. */
const CONSULTATION_FEE = 20_000;
const SERVICE_FEE = Math.round(CONSULTATION_FEE * 0.05);
const TOTAL_FEE = CONSULTATION_FEE + SERVICE_FEE;

/** Farmer asks for veterinary help on a case: type, optional specific vet, details. The fee is fixed platform-wide. */
export function RequestVetModal({ open, onOpenChange, data, preferredVetId }: { open: boolean; onOpenChange: (open: boolean) => void; data: IncidentCase; preferredVetId?: string | null }) {
  const client = useQueryClient();
  const vets = useQuery({ queryKey: qk.vetDirectory(), queryFn: () => vetsApi.directory(), enabled: open });
  const [type, setType] = useState(data.incident.severity >= 2 ? 1 : 0);
  const [vetId, setVetId] = useState(preferredVetId ?? "");
  const [description, setDescription] = useState(data.incident.evidenceSummary.slice(0, 1000));
  const [date, setDate] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});

  const create = useMutation({
    mutationFn: () =>
      vetsApi.createRequest({
        incidentId: data.incident.id,
        requestType: type,
        vetPartnerId: vetId || null,
        description: description.trim() || null,
        preferredVisitDate: date || null,
      }),
    onSuccess: async () => {
      toast.success(vetId ? "Request sent to the vet" : "Request sent to available vets");
      await Promise.all([client.invalidateQueries({ queryKey: qk.incidentCase(data.incident.id) }), client.invalidateQueries({ queryKey: ["overview"] })]);
      onOpenChange(false);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (date && date < new Date().toISOString().slice(0, 10)) found.date = "Choose today or a later date.";
    setErrors(found);
    if (!Object.keys(found).length) create.mutate();
  }

  const visit = type === 1 || type === 3;

  return (
    <Modal open={open} onOpenChange={onOpenChange} title="Ask a vet" description="Vets see the case summary and your farm's area. Your phone number is shared only with the vet who accepts.">
      <form onSubmit={submit} className="space-y-5" noValidate>
        <div className="space-y-2">
          <p className="text-sm font-semibold">What kind of help?</p>
          <ChoiceChips
            label="Request type"
            value={type}
            onChange={setType}
            options={[0, 1, 3, 2].map((t) => ({ value: t, label: requestTypeMeta[t]!.label }))}
          />
          <p className="text-[13px] text-muted">{requestTypeMeta[type]?.description}</p>
        </div>
        <Field label="Vet" hint={vets.data && vets.data.items.length === 0 ? "No vets are registered yet." : "Choose a vet you know, or let any available vet accept."}>
          <Select value={vetId} onChange={(e) => setVetId(e.target.value)}>
            <option value="">Any available vet</option>
            {vets.data?.items.map((v) => (
              <option key={v.id} value={v.id}>
                {v.name} — {v.specialization}
              </option>
            ))}
          </Select>
        </Field>
        <div className="rounded-xl border border-beige p-3 text-[13px]">
          <p className="font-semibold">Fixed consultation fee — {formatMoney(TOTAL_FEE)}</p>
          <p className="mt-1 text-muted">
            {formatMoney(CONSULTATION_FEE)} for the consultation + {formatMoney(SERVICE_FEE)} FarmHand service fee (5%). You pay in full only after a vet accepts your request.
          </p>
        </div>
        {visit && (
          <Field label="Preferred date" optional error={errors.date}>
            <Input type="date" value={date} min={new Date().toISOString().slice(0, 10)} onChange={(e) => setDate(e.target.value)} />
          </Field>
        )}
        <Field label="Message for the vet" optional>
          <Textarea value={description} onChange={(e) => setDescription(e.target.value.slice(0, 1000))} className="min-h-24" />
        </Field>
        <InlineError error={create.error} />
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" variant="accent" loading={create.isPending}>
            Send request
          </Button>
        </div>
      </form>
    </Modal>
  );
}
