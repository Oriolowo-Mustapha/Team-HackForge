"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useDeferredValue, useState } from "react";
import { toast } from "sonner";
import { vetsApi } from "@/lib/api/endpoints";
import { requestTypeMeta } from "@/lib/api/enums";
import type { VetRequest } from "@/lib/api/schemas";
import { formatDateTime, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queries";
import { TreatmentPlanModal } from "@/components/case/treatment-plan-modal";
import {
  PaymentStatusBadge,
  RequestStatusBadge,
  SeverityBadge,
} from "@/components/domain/badges";
import { VetRequestRow } from "@/components/domain/rows";
import { Button, ButtonLink } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Pager, Panel, ResourcePanel } from "@/components/ui/resource-panel";
import { Stat } from "@/components/ui/stat";
import { EmptyState, InlineError, Notice } from "@/components/ui/states";

export function VetDirectoryScreen() {
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());
  const query = useQuery({
    queryKey: qk.vetDirectory(q),
    queryFn: () => vetsApi.directory(q),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Find a veterinarian"
        subtitle="Request a remote review or farm visit from a case file."
      />
      <Input
        className="mb-5"
        type="search"
        aria-label="Search veterinarians"
        placeholder="Search by name or specialization"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <ResourcePanel query={query}>
        {(data) =>
          data.items.length ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {data.items.map((vet) => (
                <Panel key={vet.id} title={vet.name}>
                  <p className="font-medium">{vet.specialization}</p>
                  <p className="mt-2 text-sm text-muted">{vet.contact}</p>
                  <ButtonLink
                    href="/cases"
                    variant="secondary"
                    className="mt-4"
                  >
                    Request from a case
                  </ButtonLink>
                </Panel>
              ))}
            </div>
          ) : (
            <EmptyState title="No matching veterinarians">
              Try a different search, or ask your administrator about vet
              availability.
            </EmptyState>
          )
        }
      </ResourcePanel>
    </div>
  );
}

export function VetDashboardScreen() {
  const assigned = useQuery({
    queryKey: qk.vetRequests({ scope: "assigned", pageSize: 5 }),
    queryFn: () => vetsApi.requests({ scope: "assigned", pageSize: 5 }),
  });
  const open = useQuery({
    queryKey: qk.vetRequests({ scope: "open", pageSize: 1 }),
    queryFn: () => vetsApi.requests({ scope: "open", pageSize: 1 }),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Your veterinary work"
        subtitle="Review requests, keep case notes, and share a treatment plan with the farmer."
      />
      <div className="mb-5 grid grid-cols-2 gap-3">
        <Stat
          label="Assigned requests"
          value={assigned.data?.totalCount ?? "—"}
          href="/vet/requests?scope=assigned"
        />
        <Stat
          label="Open requests"
          value={open.data?.totalCount ?? "—"}
          href="/vet/requests"
        />
      </div>
      <ResourcePanel query={assigned}>
        {(data) => (
          <div className="space-y-3">
            {data.items.length ? (
              data.items.map((request) => (
                <VetRequestRow
                  key={request.id}
                  request={request}
                  href={`/vet/requests/${request.id}`}
                />
              ))
            ) : (
              <EmptyState title="No assigned requests">
                Accept an open request to start working with a farmer.
              </EmptyState>
            )}
            <ButtonLink href="/vet/requests" variant="secondary" block>
              Browse requests
            </ButtonLink>
          </div>
        )}
      </ResourcePanel>
    </div>
  );
}

export function VetRequestsScreen({
  initialScope = "open",
}: {
  initialScope?: "open" | "assigned";
}) {
  const [scope, setScope] = useState(initialScope);
  const [page, setPage] = useState(1);
  const filters = { scope, page, pageSize: 20 };
  const query = useQuery({
    queryKey: qk.vetRequests(filters),
    queryFn: () => vetsApi.requests(filters),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Vet requests"
        subtitle="Farmer contact details become available after you accept a request."
      />
      <Select
        aria-label="Request scope"
        className="mb-5"
        value={scope}
        onChange={(e) => {
          setScope(e.target.value as "open" | "assigned");
          setPage(1);
        }}
      >
        <option value="open">Open requests</option>
        <option value="assigned">Assigned to me</option>
      </Select>
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="space-y-3">
                {data.items.map((request) => (
                  <VetRequestRow
                    key={request.id}
                    request={request}
                    href={`/vet/requests/${request.id}`}
                  />
                ))}
              </div>
            ) : (
              <EmptyState title="No requests here">
                New requests will appear here when farmers ask for help.
              </EmptyState>
            )}
            <Pager
              page={page}
              totalPages={data.totalPages}
              onChange={setPage}
            />
          </>
        )}
      </ResourcePanel>
    </div>
  );
}

function RequestDetail({ request }: { request: VetRequest }) {
  const client = useQueryClient();
  const [notes, setNotes] = useState("");
  const [planOpen, setPlanOpen] = useState(false);
  const [action, setAction] = useState<
    "accept" | "decline" | "complete" | null
  >(null);
  const mutation = useMutation({
    mutationFn: async (selected: "accept" | "decline" | "complete") =>
      selected === "accept"
        ? vetsApi.accept(request.id)
        : selected === "decline"
          ? vetsApi.decline(request.id, notes.trim() || null)
          : vetsApi.complete(request.id, notes.trim() || null),
    onSuccess: async () => {
      setAction(null);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["vet-requests"] }),
        client.invalidateQueries({ queryKey: qk.vetRequest(request.id) }),
        client.invalidateQueries({
          queryKey: qk.incidentCase(request.incidentId),
        }),
      ]);
      toast.success("Request updated");
    },
  });
  const canViewCase =
    request.availableActions.includes("treatment_plan") || request.status === 2;
  return (
    <div className="space-y-4">
      <Panel title={request.incidentType}>
        <div className="mb-4 flex flex-wrap gap-2">
          <SeverityBadge severity={request.incidentSeverity} />
          <RequestStatusBadge status={request.status} />
          <PaymentStatusBadge
            status={request.paymentStatus}
            mode={request.latestPayment?.mode}
          />
        </div>
        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-muted">Farm</dt>
            <dd>
              {request.farmName} · {request.farmLocation}
            </dd>
          </div>
          <div>
            <dt className="text-muted">Animal or group</dt>
            <dd>{request.subjectName ?? "Whole farm"}</dd>
          </div>
          <div>
            <dt className="text-muted">Consultation</dt>
            <dd>
              {requestTypeMeta[request.requestType]?.label} ·{" "}
              {formatMoney(request.farmerCost, request.currency)}
            </dd>
          </div>
          <div>
            <dt className="text-muted">Preferred visit</dt>
            <dd>{formatDateTime(request.preferredVisitDate)}</dd>
          </div>
        </dl>
        {request.description && (
          <p className="mt-4 whitespace-pre-wrap text-sm">
            {request.description}
          </p>
        )}
        {request.completionNotes && (
          <p className="mt-4 text-sm">
            Completion notes: {request.completionNotes}
          </p>
        )}
      </Panel>
      <Panel title="Farmer contact">
        {request.farmer ? (
          <div className="space-y-2 text-sm">
            <p>{request.farmer.name}</p>
            <p>{request.farmer.email}</p>
            <p>{request.farmer.phone ?? "No phone number supplied"}</p>
          </div>
        ) : (
          <p className="text-sm text-muted">
            Contact information is private until you accept this request.
          </p>
        )}
      </Panel>
      <div className="flex flex-wrap gap-2">
        {(["accept", "decline", "complete"] as const)
          .filter((item) => request.availableActions.includes(item))
          .map((item) => (
            <Button
              key={item}
              variant={item === "decline" ? "secondary" : "primary"}
              onClick={() => setAction(item)}
            >
              {item === "accept"
                ? "Accept request"
                : item === "decline"
                  ? "Decline request"
                  : "Complete consultation"}
            </Button>
          ))}
        {request.availableActions.includes("treatment_plan") && (
          <Button variant="secondary" onClick={() => setPlanOpen(true)}>
            Write treatment plan
          </Button>
        )}
        {canViewCase && (
          <ButtonLink href={`/cases/${request.incidentId}`} variant="secondary">
            Open case file
          </ButtonLink>
        )}
      </div>
      {request.status === 1 &&
        request.farmerCost > 0 &&
        request.paymentStatus !== "Paid" && (
          <Notice tone="warning">
            The consultation can be completed after the farmer&apos;s payment is
            verified.
          </Notice>
        )}
      {(request.availableActions.includes("complete") ||
        request.availableActions.includes("decline")) && (
        <Field label="Consultation notes or reason" optional>
          <Textarea
            maxLength={2000}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </Field>
      )}
      <InlineError error={mutation.error} />
      <ConfirmDialog
        open={action !== null}
        onOpenChange={(open) => {
          if (!open) setAction(null);
        }}
        title={
          action === "accept"
            ? "Accept this request?"
            : action === "decline"
              ? "Decline this request?"
              : "Complete this consultation?"
        }
        description={
          action === "accept"
            ? "You will become responsible for this request and receive the farmer's contact information."
            : action === "decline"
              ? "The farmer will see that you declined this request."
              : "Confirm that the consultation and treatment advice have been provided."
        }
        confirmLabel="Confirm"
        tone={action === "decline" ? "danger" : "primary"}
        loading={mutation.isPending}
        onConfirm={() => {
          if (action) mutation.mutate(action);
        }}
      />
      <TreatmentPlanModal
        open={planOpen}
        onOpenChange={setPlanOpen}
        vetRequestId={request.id}
        incidentId={request.incidentId}
      />
    </div>
  );
}

export function VetRequestScreen({ id }: { id: string }) {
  const query = useQuery({
    queryKey: qk.vetRequest(id),
    queryFn: () => vetsApi.request(id),
  });
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title="Consultation request" back="/vet/requests" />
      <ResourcePanel query={query}>
        {(request) => <RequestDetail request={request} />}
      </ResourcePanel>
    </div>
  );
}

function PayoutForm({
  configured,
  initialBank,
  initialName,
  initialCountry,
}: {
  configured: boolean;
  initialBank: string;
  initialName: string;
  initialCountry: string;
}) {
  const client = useQueryClient();
  const [bankCode, setBank] = useState(initialBank);
  const [accountName, setName] = useState(initialName);
  const [accountNumber, setNumber] = useState("");
  const [country, setCountry] = useState(initialCountry);
  const save = useMutation({
    mutationFn: () =>
      vetsApi.updatePayoutAccount({
        bankCode: bankCode.trim(),
        accountName: accountName.trim(),
        accountNumber: accountNumber.trim(),
        country,
      }),
    onSuccess: async () => {
      setNumber("");
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.payoutAccount }),
        client.invalidateQueries({ queryKey: qk.session }),
      ]);
      toast.success("Payout account saved");
    },
  });
  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <Notice>
        Account details are encrypted by the server.{" "}
        {configured
          ? "Enter the full account number to replace the existing account."
          : "Add the bank account that should receive live consultation payouts."}
      </Notice>
      <Field label="Bank code">
        <Input
          required
          maxLength={20}
          value={bankCode}
          onChange={(e) => setBank(e.target.value)}
        />
      </Field>
      <Field label="Account name">
        <Input
          required
          maxLength={150}
          value={accountName}
          onChange={(e) => setName(e.target.value)}
        />
      </Field>
      <Field label="Account number">
        <Input
          required
          inputMode="numeric"
          autoComplete="off"
          maxLength={30}
          value={accountNumber}
          onChange={(e) => setNumber(e.target.value)}
        />
      </Field>
      <Field label="Country">
        <Select value={country} onChange={(e) => setCountry(e.target.value)}>
          <option value="NG">Nigeria</option>
          <option value="GH">Ghana</option>
          <option value="KE">Kenya</option>
        </Select>
      </Field>
      <InlineError error={save.error} />
      <Button type="submit" loading={save.isPending}>
        Save payout account
      </Button>
    </form>
  );
}

export function PayoutScreen() {
  const query = useQuery({
    queryKey: qk.payoutAccount,
    queryFn: vetsApi.payoutAccount,
  });
  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Payout account"
        subtitle="Your consultation settlement details."
      />
      <ResourcePanel query={query}>
        {(data) => (
          <Panel>
            {data.configured && (
              <p className="mb-4 text-sm">
                Current account: {data.accountName} · {data.accountNumberMasked}
              </p>
            )}
            <PayoutForm
              configured={data.configured}
              initialBank={data.bankCode ?? ""}
              initialName={data.accountName ?? ""}
              initialCountry={data.country ?? "NG"}
            />
          </Panel>
        )}
      </ResourcePanel>
    </div>
  );
}
