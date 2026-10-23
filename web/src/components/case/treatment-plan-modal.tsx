"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2 } from "lucide-react";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { vetsApi } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Field, Input, Textarea } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";

const today = () => new Date().toISOString().slice(0, 10);

/** Vet writes a treatment plan with a dated schedule the farmer can tick off. */
export function TreatmentPlanModal({ open, onOpenChange, vetRequestId, incidentId }: { open: boolean; onOpenChange: (open: boolean) => void; vetRequestId: string; incidentId: string }) {
  const client = useQueryClient();
  const [instructions, setInstructions] = useState("");
  const [steps, setSteps] = useState([{ action: "", dueDate: today() }]);
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () =>
      vetsApi.createTreatmentPlan({
        vetRequestId,
        instructionsText: instructions.trim(),
        language: null,
        schedule: steps.filter((s) => s.action.trim()).map((s) => ({ action: s.action.trim(), dueDate: s.dueDate })),
      }),
    onSuccess: async () => {
      toast.success("Treatment plan shared with the farmer");
      await client.invalidateQueries({ queryKey: qk.incidentCase(incidentId) });
      onOpenChange(false);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    if (instructions.trim().length < 10) {
      setError("Write the instructions the farmer should follow.");
      return;
    }
    setError(null);
    save.mutate();
  }

  return (
    <Modal open={open} onOpenChange={onOpenChange} title="Treatment plan" description="The farmer sees this in the case and can tick off each step.">
      <form onSubmit={submit} className="space-y-5" noValidate>
        <Field label="Instructions" error={error}>
          <Textarea value={instructions} onChange={(e) => setInstructions(e.target.value.slice(0, 2000))} className="min-h-36" placeholder="Medication, dose and duration, isolation, feeding and when to call you." />
        </Field>
        <div className="space-y-2">
          <p className="text-sm font-semibold">Schedule</p>
          {steps.map((step, i) => (
            <div key={i} className="flex gap-2">
              <Input
                aria-label={`Step ${i + 1}`}
                value={step.action}
                onChange={(e) => setSteps((list) => list.map((s, j) => (j === i ? { ...s, action: e.target.value.slice(0, 200) } : s)))}
                placeholder="e.g. Start medicated water (day 1 of 5)"
              />
              <Input
                aria-label={`Step ${i + 1} due date`}
                type="date"
                value={step.dueDate}
                onChange={(e) => setSteps((list) => list.map((s, j) => (j === i ? { ...s, dueDate: e.target.value } : s)))}
                className="w-40 shrink-0"
              />
              <button
                type="button"
                onClick={() => setSteps((list) => list.filter((_, j) => j !== i))}
                disabled={steps.length === 1}
                className="inline-flex size-12 shrink-0 items-center justify-center rounded-2xl text-muted hover:bg-sand disabled:opacity-30"
                aria-label={`Remove step ${i + 1}`}
              >
                <Trash2 className="size-4" />
              </button>
            </div>
          ))}
          <Button size="sm" variant="ghost" icon={<Plus className="size-4" />} onClick={() => setSteps((list) => [...list, { action: "", dueDate: today() }])}>
            Add step
          </Button>
        </div>
        <InlineError error={save.error} />
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" loading={save.isPending}>
            Share plan
          </Button>
        </div>
      </form>
    </Modal>
  );
}
