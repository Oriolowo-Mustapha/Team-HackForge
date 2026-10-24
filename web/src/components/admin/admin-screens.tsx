"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  useDeferredValue,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import { toast } from "sonner";
import { adminApi, systemApi } from "@/lib/api/endpoints";
import type { AdminUser, AdminVet, SystemInfo } from "@/lib/api/schemas";
import { formatDateTime, formatMoney } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { IncidentRow } from "@/components/domain/rows";
import { PaymentCard } from "@/components/payments/payment-screens";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog, Modal } from "@/components/ui/dialog";
import { Field, Input, PasswordInput, Select } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Pager, Panel, ResourcePanel } from "@/components/ui/resource-panel";
import { Stat } from "@/components/ui/stat";
import { EmptyState, InlineError, Notice } from "@/components/ui/states";

function Table({
  headings,
  children,
}: {
  headings: string[];
  children: ReactNode;
}) {
  return (
    <div className="overflow-x-auto rounded-[24px] bg-paper ring-1 ring-line/60">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-line">
          <tr>
            {headings.map((heading) => (
              <th
                key={heading}
                scope="col"
                className="whitespace-nowrap px-4 py-3 font-semibold"
              >
                {heading}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="[&_td]:px-4 [&_td]:py-4 [&_tr]:border-b [&_tr]:border-line/60">
          {children}
        </tbody>
      </table>
    </div>
  );
}

function Integrations({ info }: { info: SystemInfo }) {
  const rows = [
    [
      "AI text analysis",
      info.aiTextConfigured
        ? "Configured"
        : info.ruleBasedDemoFallbackEnabled
          ? "Rule-based fallback"
          : "Not configured",
    ],
    [
      "AI photo analysis",
      info.aiVisionConfigured ? "Configured" : "Not configured",
    ],
    [
      "Voice transcription",
      info.transcriptionConfigured ? "Configured" : "Not configured",
    ],
    ["Payments", info.paymentsMode],
    ["Email delivery", info.emailEnabled ? "Enabled" : "Disabled"],
    ["Image storage", info.imageStorage],
    ["Demo data", info.demoMode ? "Enabled" : "Disabled"],
  ];
  return (
    <dl className="divide-y divide-line/60">
      {rows.map(([label, value]) => (
        <div
          key={label}
          className="flex flex-wrap justify-between gap-2 py-3 text-sm"
        >
          <dt>{label}</dt>
          <dd className="font-semibold">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function AdminOverviewScreen() {
  const query = useQuery({
    queryKey: qk.admin.overview,
    queryFn: adminApi.overview,
  });
  return (
    <div className="space-y-5">
      <PageHeader
        title="Platform overview"
        subtitle="Operational counts, verified payments, and recent administrative events."
      />
      <ResourcePanel query={query}>
        {(data) => (
          <>
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <Stat
                label="Active users"
                value={data.counts.activeUsers}
                href="/admin/users"
              />
              <Stat
                label="Farms"
                value={data.counts.farms}
                href="/admin/farms"
              />
              <Stat
                label="Animals"
                value={data.counts.animals + data.counts.flockHeadcount}
              />
              <Stat
                label="Live payments"
                value={formatMoney(data.counts.completedLivePaymentsTotal)}
                context="Verified live transactions"
                href="/admin/payments"
              />
            </div>
            <Notice tone="demo">
              Simulated payments total{" "}
              {formatMoney(data.counts.completedSimulatedPaymentsTotal)}. These
              transactions move no money.
            </Notice>
            <div className="grid gap-4 lg:grid-cols-2">
              <Panel title="Integrations">
                <Integrations info={data.integrations} />
              </Panel>
              <Panel title="Recent events">
                {data.recentEvents.length ? (
                  <ul className="divide-y divide-line/60">
                    {data.recentEvents.map((event) => (
                      <li key={event.id} className="py-3">
                        <p className="text-sm font-semibold">
                          {event.eventType}
                        </p>
                        <p className="text-xs text-muted">
                          {formatDateTime(event.timestamp)}
                        </p>
                        <p className="mt-1 break-words text-sm">
                          {event.details}
                        </p>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-muted">No events yet.</p>
                )}
              </Panel>
            </div>
          </>
        )}
      </ResourcePanel>
    </div>
  );
}

export function AdminUsersScreen() {
  const client = useQueryClient();
  const { user } = useSession();
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());
  const [role, setRole] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AdminUser | null>(null);
  const filters = { q, role: role || undefined, page, pageSize: 25 };
  const query = useQuery({
    queryKey: qk.admin.users(filters),
    queryFn: () => adminApi.users(filters),
  });
  const toggle = useMutation({
    mutationFn: (item: AdminUser) =>
      adminApi.setUserActive(item.id, !item.isActive),
    onSuccess: async () => {
      setSelected(null);
      await client.invalidateQueries({ queryKey: ["admin"] });
      toast.success("User access updated");
    },
  });
  return (
    <div>
      <PageHeader
        title="Users"
        subtitle="Manage account access. Deactivating an account invalidates its sessions."
      />
      <div className="mb-5 grid gap-3 sm:grid-cols-2">
        <Input
          type="search"
          aria-label="Search users"
          placeholder="Search name or email"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <Select
          aria-label="User role"
          value={role}
          onChange={(e) => {
            setRole(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All roles</option>
          {["Farmer", "Vet", "Admin"].map((item) => (
            <option key={item}>{item}</option>
          ))}
        </Select>
      </div>
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <Table headings={["Account", "Role", "Status", "Access"]}>
                {data.items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <p className="font-semibold">
                        {item.fullName ?? item.email}
                      </p>
                      <p className="text-xs text-muted">{item.email}</p>
                    </td>
                    <td>{item.role}</td>
                    <td>
                      <Badge tone={item.isActive ? "healthy" : "neutral"}>
                        {item.isActive ? "Active" : "Inactive"}
                      </Badge>
                      {item.mustChangePassword && (
                        <p className="mt-1 text-xs">Password change required</p>
                      )}
                    </td>
                    <td>
                      <Button
                        size="sm"
                        variant="secondary"
                        disabled={item.id === user?.userId}
                        onClick={() => setSelected(item)}
                      >
                        {item.isActive ? "Deactivate" : "Activate"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </Table>
            ) : (
              <EmptyState title="No matching users" />
            )}
            <Pager
              page={page}
              totalPages={data.totalPages}
              onChange={setPage}
            />
          </>
        )}
      </ResourcePanel>
      <InlineError error={toggle.error} className="mt-4" />
      <ConfirmDialog
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title={`${selected?.isActive ? "Deactivate" : "Activate"} this account?`}
        description={selected?.email}
        confirmLabel={
          selected?.isActive ? "Deactivate account" : "Activate account"
        }
        tone={selected?.isActive ? "danger" : "primary"}
        loading={toggle.isPending}
        onConfirm={() => {
          if (selected) toggle.mutate(selected);
        }}
      />
    </div>
  );
}

function RegisterVetForm({
  emailEnabled,
  onSaved,
}: {
  emailEnabled: boolean;
  onSaved: () => void;
}) {
  const [form, setForm] = useState({
    email: "",
    fullName: "",
    specialization: "",
    contact: "",
    phoneNumber: "",
    temporaryPassword: "",
  });
  const update =
    (key: keyof typeof form) => (e: { target: { value: string } }) =>
      setForm((current) => ({ ...current, [key]: e.target.value }));
  const save = useMutation({
    mutationFn: () =>
      adminApi.registerVet({
        ...form,
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        temporaryPassword: form.temporaryPassword || null,
        phoneNumber: form.phoneNumber.trim() || null,
      }),
    onSuccess: (result) => {
      toast.success(result.message);
      onSaved();
    },
  });
  return (
    <form
      className="space-y-4"
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <Field label="Full name">
        <Input
          required
          maxLength={150}
          value={form.fullName}
          onChange={update("fullName")}
        />
      </Field>
      <Field label="Email">
        <Input
          required
          type="email"
          maxLength={254}
          autoComplete="off"
          value={form.email}
          onChange={update("email")}
        />
      </Field>
      <Field label="Specialization">
        <Input
          required
          maxLength={200}
          value={form.specialization}
          onChange={update("specialization")}
        />
      </Field>
      <Field label="Professional contact details">
        <Input
          required
          maxLength={300}
          value={form.contact}
          onChange={update("contact")}
        />
      </Field>
      <Field label="Phone number" optional>
        <Input
          type="tel"
          maxLength={30}
          value={form.phoneNumber}
          onChange={update("phoneNumber")}
        />
      </Field>
      <Field
        label="Temporary password"
        optional={emailEnabled}
        hint={
          emailEnabled
            ? "Leave blank to generate and email a temporary password."
            : "Email is disabled. Supply a password and share it with the vet securely. The vet must change it at first sign-in."
        }
      >
        <PasswordInput
          required={!emailEnabled}
          autoComplete="new-password"
          minLength={8}
          maxLength={72}
          value={form.temporaryPassword}
          onChange={update("temporaryPassword")}
        />
      </Field>
      <InlineError error={save.error} />
      <Button type="submit" block loading={save.isPending}>
        Register veterinarian
      </Button>
    </form>
  );
}

export function AdminVetsScreen() {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [registerOpen, setRegister] = useState(false);
  const [selected, setSelected] = useState<AdminVet | null>(null);
  const [resetVet, setResetVet] = useState<AdminVet | null>(null);
  const [password, setPassword] = useState("");
  const query = useQuery({
    queryKey: [...qk.admin.vets, page],
    queryFn: () => adminApi.vets(page),
  });
  const system = useQuery({ queryKey: qk.system, queryFn: systemApi.info });
  const invalidate = () => client.invalidateQueries({ queryKey: ["admin"] });
  const toggle = useMutation({
    mutationFn: (vet: AdminVet) =>
      vet.isActive
        ? adminApi.deactivateVet(vet.id)
        : adminApi.activateVet(vet.id),
    onSuccess: async () => {
      setSelected(null);
      await invalidate();
    },
  });
  const reset = useMutation({
    mutationFn: () => {
      if (!resetVet) throw new Error("Select a vet.");
      return adminApi.resetVetCredentials(resetVet.id, password || null);
    },
    onSuccess: async (result) => {
      toast.success(result.message);
      setResetVet(null);
      setPassword("");
      await invalidate();
    },
  });
  return (
    <div>
      <PageHeader
        title="Veterinarians"
        subtitle="Verify clinicians before creating their account."
        actions={
          <Button onClick={() => setRegister(true)}>Register vet</Button>
        }
      />
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <Table
                headings={[
                  "Veterinarian",
                  "Specialization",
                  "Status",
                  "Actions",
                ]}
              >
                {data.items.map((vet) => (
                  <tr key={vet.id}>
                    <td>
                      <p className="font-semibold">{vet.name}</p>
                      <p className="text-xs text-muted">{vet.email}</p>
                    </td>
                    <td>{vet.specialization}</td>
                    <td>{vet.isActive ? "Active" : "Inactive"}</td>
                    <td>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => setSelected(vet)}
                        >
                          {vet.isActive ? "Deactivate" : "Activate"}
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => {
                            reset.reset();
                            setPassword("");
                            setResetVet(vet);
                          }}
                        >
                          Reset credentials
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </Table>
            ) : (
              <EmptyState title="No veterinarians registered" />
            )}
            <Pager
              page={page}
              totalPages={data.totalPages}
              onChange={setPage}
            />
          </>
        )}
      </ResourcePanel>
      <InlineError error={toggle.error} className="mt-4" />
      <ConfirmDialog
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title={`${selected?.isActive ? "Deactivate" : "Activate"} this veterinarian?`}
        description={selected?.name}
        confirmLabel="Confirm access change"
        loading={toggle.isPending}
        onConfirm={() => {
          if (selected) toggle.mutate(selected);
        }}
      />
      <Modal
        open={registerOpen}
        onOpenChange={setRegister}
        title="Register veterinarian"
        description="Temporary credentials require a password change before access to clinical work."
      >
        <RegisterVetForm
          emailEnabled={system.data?.emailEnabled ?? false}
          onSaved={() => {
            setRegister(false);
            void invalidate();
          }}
        />
      </Modal>
      <Modal
        open={resetVet !== null}
        onOpenChange={(open) => {
          if (!open) {
            setResetVet(null);
            setPassword("");
          }
        }}
        title="Reset vet credentials"
        description="Existing sessions are revoked. The vet must change the temporary password."
      >
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            reset.mutate();
          }}
        >
          <p>{resetVet?.email}</p>
          <Field
            label="Temporary password"
            optional={system.data?.emailEnabled}
            hint={
              system.data?.emailEnabled
                ? "Leave blank to generate and email a password."
                : "Share the supplied password securely with the vet."
            }
          >
            <PasswordInput
              required={!system.data?.emailEnabled}
              autoComplete="new-password"
              minLength={8}
              maxLength={72}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </Field>
          <InlineError error={reset.error} />
          <Button type="submit" loading={reset.isPending}>
            Reset credentials
          </Button>
        </form>
      </Modal>
    </div>
  );
}

export function AdminFarmsScreen() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());
  const filters = { q, page };
  const query = useQuery({
    queryKey: qk.admin.farms(filters),
    queryFn: () => adminApi.farms(filters),
  });
  return (
    <div>
      <PageHeader title="Farms" subtitle="Registered farms and their owners." />
      <Input
        className="mb-5"
        aria-label="Search farms"
        type="search"
        value={search}
        placeholder="Search farms"
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <Table headings={["Farm", "Location", "Owner", "Created"]}>
                {data.items.map((farm) => (
                  <tr key={farm.id}>
                    <td className="font-semibold">{farm.displayName}</td>
                    <td>{farm.location}</td>
                    <td>
                      <p>{farm.ownerName}</p>
                      <p className="text-xs text-muted">{farm.ownerEmail}</p>
                    </td>
                    <td>{formatDateTime(farm.createdAt)}</td>
                  </tr>
                ))}
              </Table>
            ) : (
              <EmptyState title="No matching farms" />
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

export function AdminCasesScreen() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());
  const filters = { q, page, pageSize: 25 };
  const query = useQuery({
    queryKey: qk.admin.incidents(filters),
    queryFn: () => adminApi.incidents(filters),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="All cases"
        subtitle="Case files across the platform."
      />
      <Input
        className="mb-5"
        aria-label="Search platform cases"
        type="search"
        placeholder="Search cases"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="space-y-3">
                {data.items.map((item) => (
                  <IncidentRow key={item.incident.id} item={item} showFarm />
                ))}
              </div>
            ) : (
              <EmptyState title="No matching cases" />
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

export function AdminPaymentsScreen() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const filters = { page, status: status || undefined };
  const query = useQuery({
    queryKey: qk.admin.payments(filters),
    queryFn: () => adminApi.payments(filters),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Platform payments"
        subtitle="Monitor live and simulated consultation payments."
      />
      <Select
        aria-label="Payment status"
        className="mb-5"
        value={status}
        onChange={(e) => {
          setStatus(e.target.value);
          setPage(1);
        }}
      >
        <option value="">All statuses</option>
        {["Pending", "Completed", "Failed", "Cancelled"].map((item) => (
          <option key={item}>{item}</option>
        ))}
      </Select>
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="space-y-3">
                {data.items.map((payment) => (
                  <PaymentCard key={payment.id} payment={payment} />
                ))}
              </div>
            ) : (
              <EmptyState title="No payments found" />
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

export function AdminAuditScreen() {
  const [page, setPage] = useState(1);
  const [eventType, setEventType] = useState("");
  const eventFilter = useDeferredValue(eventType.trim());
  const filters = { eventType: eventFilter || undefined, page };
  const query = useQuery({
    queryKey: qk.admin.audit(filters),
    queryFn: () => adminApi.audit(filters),
  });
  return (
    <div>
      <PageHeader
        title="Audit log"
        subtitle="Administrative and clinical actions recorded by the platform."
      />
      <Input
        className="mb-5"
        aria-label="Filter event type"
        placeholder="Event type, for example payment.initiated"
        value={eventType}
        onChange={(e) => {
          setEventType(e.target.value);
          setPage(1);
        }}
      />
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <Table headings={["Time", "Event", "Actor", "Details"]}>
                {data.items.map((event) => (
                  <tr key={event.id}>
                    <td className="whitespace-nowrap">
                      {formatDateTime(event.timestamp)}
                    </td>
                    <td>{event.eventType}</td>
                    <td className="break-all text-xs">{event.actorId}</td>
                    <td className="max-w-sm break-words">
                      {event.details ?? "—"}
                    </td>
                  </tr>
                ))}
              </Table>
            ) : (
              <EmptyState title="No matching events" />
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

export function AdminSystemScreen() {
  const query = useQuery({
    queryKey: qk.admin.system,
    queryFn: adminApi.system,
    refetchInterval: 30_000,
  });
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="System health"
        subtitle="Database and service configuration. Refreshed every 30 seconds."
      />
      <ResourcePanel query={query}>
        {(data) => (
          <div className="space-y-4">
            <Panel title="Database">
              <Badge tone={data.database.connected ? "healthy" : "failed"}>
                {data.database.connected ? "Connected" : "Unavailable"}
              </Badge>
              <p className="mt-3 text-sm">
                {data.database.appliedMigrations} migrations applied;{" "}
                {data.database.pendingMigrations.length} pending.
              </p>
              {data.database.pendingMigrations.length > 0 && (
                <Notice tone="warning">
                  Apply the pending migrations before serving traffic.
                </Notice>
              )}
            </Panel>
            <Panel title="Services">
              <Integrations info={data.integrations} />
            </Panel>
            <p className="text-xs text-muted">
              Server time: {formatDateTime(data.serverTimeUtc)} · Version:{" "}
              {data.version ?? "Unknown"}
            </p>
          </div>
        )}
      </ResourcePanel>
    </div>
  );
}
