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
import { ChoiceChips, Field, Input, Select, Textarea } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";

const SUGGESTED_FEE: Record<number, number> = { 0: 3000, 1: 10000, 2: 5000, 3: 7000 };

/** Farmer asks for veterinary help on a case: type, optional specific vet, offered fee, details. */
export function RequestVetModal({ open, onOpenChange, data, preferredVetId }: { open: boolean; onOpenChange: (open: boolean) => void; data: IncidentCase; preferredVetId?: string | null }) {
  const client = useQueryClient();
  const vets = useQuery({ queryKey: qk.vetDirectory(), queryFn: () => vetsApi.directory(), enabled: open });
  const [type, setType] = useState(data.incident.severity >= 2 ? 1 : 0);
  const [vetId, setVetId] = useState(preferredVetId ?? "");
  const [fee, setFee] = useState(String(SUGGESTED_FEE[data.incident.severity >= 2 ? 1 : 0]));
  const [feeTouched, setFeeTouched] = useState(false);
  const [description, setDescription] = useState(data.incident.evidenceSummary.slice(0, 1000));
  const [date, setDate] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});

  const create = useMutation({
    mutationFn: () =>
      vetsApi.createRequest({
        incidentId: data.incident.id,
        requestType: type,
        farmerCost: Number(fee || 0),
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
    const amount = Number(fee || 0);
    const found: Record<string, string> = {};
    if (!Number.isFinite(amount) || (amount !== 0 && (amount < 100 || amount > 5_000_000))) found.fee = "Enter 0 for no fee, or between ₦100 and ₦5,000,000.";
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
            onChange={(t) => {
              setType(t);
              if (!feeTouched) setFee(String(SUGGESTED_FEE[t] ?? 5000));
            }}
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
        <Field label="Fee you're offering (₦)" hint="The vet sees this before accepting. You pay only after a vet accepts, and the full total — including FarmHand's 5% service fee — is shown before you pay. Enter 0 for no fee." error={errors.fee}>
          <Input
            inputMode="numeric"
            value={fee}
            onChange={(e) => {
              setFeeTouched(true);
              setFee(e.target.value.replace(/[^\d]/g, "").slice(0, 7));
            }}
          />
        </Field>
        {Number(fee) > 0 && <p className="-mt-3 text-[13px] font-semibold text-muted">Offering {formatMoney(Number(fee))}</p>}
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
