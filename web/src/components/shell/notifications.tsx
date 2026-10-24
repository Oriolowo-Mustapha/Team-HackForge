"use client";

import { useQuery } from "@tanstack/react-query";
import { Bell, CalendarClock, ChevronRight, ClipboardCheck, Inbox, Stethoscope, Wallet } from "lucide-react";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { farmsApi, vetsApi } from "@/lib/api/endpoints";
import { RequestStatus, requestTypeMeta } from "@/lib/api/enums";
import type { SessionUser } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";
import { formatMoney, formatRelative } from "@/lib/format";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { qk } from "@/lib/queries";
import { CountBadge } from "@/components/ui/badge";
import { Modal } from "@/components/ui/dialog";

type Item = { id: string; href: string; icon: ReactNode; title: string; detail: string; urgent?: boolean };

function useFarmerItems(enabled: boolean): Item[] {
  const { farm } = useActiveFarm();
  const { data } = useQuery({
    queryKey: qk.overview(farm?.id ?? "none"),
    queryFn: ({ signal }) => farmsApi.overview(farm!.id, signal),
    enabled: enabled && Boolean(farm),
    refetchInterval: 60_000,
  });
  if (!data) return [];

  const items: Item[] = [];
  for (const o of data.pendingObservations) {
    items.push({
      id: `obs-${o.id}`,
      href: `/reports/${o.id}`,
      icon: <ClipboardCheck />,
      title: `Review the report for ${o.subjectName ?? "your animals"}`,
      detail: `Recorded ${formatRelative(o.recordedAt)} · not saved to a case until you confirm`,
    });
  }
  for (const r of data.activeVetRequests) {
    if (r.availableActions.includes("pay")) {
      items.push({
        id: `pay-${r.id}`,
        href: `/pay/${r.id}`,
        icon: <Wallet />,
        title: `${r.vet?.name ?? "A vet"} accepted your request`,
        detail: `Pay the fixed ${formatMoney(r.farmerCost, r.currency)} consultation fee (+FarmHand's 5% service) to confirm the ${requestTypeMeta[r.requestType]?.label.toLowerCase() ?? "visit"}`,
        urgent: true,
      });
    } else if (r.status === RequestStatus.Responded) {
      items.push({
        id: `req-${r.id}`,
        href: `/cases/${r.incidentId}`,
        icon: <Stethoscope />,
        title: `${r.vet?.name ?? "A vet"} is handling ${r.incidentType}`,
        detail: `Accepted ${formatRelative(r.respondedAt)}`,
      });
    }
  }
  for (const reminder of data.reminders) {
    items.push({
      id: `rem-${reminder.incidentId}-${reminder.kind}`,
      href: `/cases/${reminder.incidentId}`,
      icon: <CalendarClock />,
      title: reminder.title,
      detail: reminder.isOverdue ? `Overdue · was due ${formatRelative(reminder.dueAt)}` : `Due ${formatRelative(reminder.dueAt)}`,
      urgent: reminder.isOverdue,
    });
  }
  return items;
}

function useVetItems(enabled: boolean): Item[] {
  const open = useQuery({
    queryKey: qk.vetRequests({ scope: "open", status: RequestStatus.Pending, pageSize: 20 }),
    queryFn: () => vetsApi.requests({ scope: "open", status: RequestStatus.Pending, pageSize: 20 }),
    enabled,
    refetchInterval: 60_000,
  });
  return (open.data?.items ?? []).map((r) => ({
    id: r.id,
    href: `/vet/requests/${r.id}`,
    icon: <Inbox />,
    title: `${r.isTargeted ? "Requested you: " : "New request: "}${r.incidentType}`,
    detail: `${r.farmLocation} · ${requestTypeMeta[r.requestType]?.label ?? "Request"} · ${formatRelative(r.createdAt)}`,
    urgent: r.isTargeted,
  }));
}

/** Bell with a count of things that need the user's attention, opening a sheet of links. */
export function NotificationsButton({ user, tone = "paper" }: { user: SessionUser; tone?: "paper" | "dark" }) {
  const [open, setOpen] = useState(false);
  const farmerItems = useFarmerItems(user.role === "Farmer");
  const vetItems = useVetItems(user.role === "Vet");
  const items = user.role === "Farmer" ? farmerItems : vetItems;

  if (user.role === "Admin") return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={items.length ? `Notifications, ${items.length} need attention` : "Notifications"}
        className={cn(
          "relative inline-flex size-11 shrink-0 items-center justify-center rounded-2xl transition-colors",
          tone === "paper" ? "bg-paper text-ink shadow-soft hover:bg-white" : "bg-cream/8 text-cream hover:bg-cream/14",
        )}
      >
        <Bell className="size-5" aria-hidden />
        {items.length > 0 && <CountBadge count={items.length} className="absolute -right-1 -top-1 ring-2 ring-cream" />}
      </button>
      <Modal open={open} onOpenChange={setOpen} title="Needs your attention" description={items.length ? undefined : "You're all caught up."}>
        {items.length > 0 && (
          <ul className="-mx-2 space-y-1">
            {items.map((item) => (
              <li key={item.id}>
                <Link href={item.href} onClick={() => setOpen(false)} className="flex items-center gap-3 rounded-2xl px-2 py-2.5 hover:bg-sand">
                  <span
                    className={cn(
                      "flex size-11 shrink-0 items-center justify-center rounded-2xl [&_svg]:size-5",
                      item.urgent ? "bg-terracotta-soft text-terracotta-deep" : "bg-sand text-ink",
                    )}
                    aria-hidden
                  >
                    {item.icon}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-[15px] font-semibold leading-snug">{item.title}</span>
                    <span className="block text-[13px] text-muted">{item.detail}</span>
                  </span>
                  <ChevronRight className="size-4 shrink-0 text-faint" aria-hidden />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </>
  );
}
