import { Activity, ClipboardList, FileText, House, Inbox, LayoutDashboard, ScanLine, ScrollText, Server, Settings, Stethoscope, Tractor, Users, Wallet, type LucideIcon } from "lucide-react";
import type { Role } from "@/lib/roles";

export type NavItem = { href: string; label: string; icon: LucideIcon; match?: (path: string) => boolean; primary?: boolean };

const starts = (prefix: string) => (path: string) => path === prefix || path.startsWith(`${prefix}/`);

export const navByRole: Record<Role, { mobile: NavItem[]; desktop: NavItem[]; primaryAction?: NavItem }> = {
  Farmer: {
    mobile: [
      { href: "/home", label: "Home", icon: House, match: starts("/home") },
      { href: "/animals", label: "Animals", icon: Tractor, match: starts("/animals") },
      { href: "/scan", label: "Report", icon: ScanLine, match: starts("/scan"), primary: true },
      { href: "/cases", label: "Cases", icon: ClipboardList, match: (p) => starts("/cases")(p) || starts("/pay")(p) },
      { href: "/vets", label: "Vets", icon: Stethoscope, match: starts("/vets") },
    ],
    desktop: [
      { href: "/home", label: "Home", icon: House, match: starts("/home") },
      { href: "/animals", label: "Animals & flocks", icon: Tractor, match: starts("/animals") },
      { href: "/reports", label: "Health reports", icon: FileText, match: starts("/reports") },
      { href: "/cases", label: "Cases", icon: ClipboardList, match: starts("/cases") },
      { href: "/vets", label: "Vets", icon: Stethoscope, match: starts("/vets") },
      { href: "/payments", label: "Payments", icon: Wallet, match: (p) => starts("/payments")(p) || starts("/pay")(p) },
      { href: "/profile", label: "Profile & farm", icon: Settings, match: starts("/profile") },
    ],
    primaryAction: { href: "/scan", label: "Report a concern", icon: ScanLine },
  },
  Vet: {
    mobile: [
      { href: "/vet", label: "Today", icon: House, match: (p) => p === "/vet" },
      { href: "/vet/requests", label: "Requests", icon: Inbox, match: starts("/vet/requests") },
      { href: "/cases", label: "Cases", icon: ClipboardList, match: starts("/cases") },
      { href: "/payments", label: "Earnings", icon: Wallet, match: (p) => starts("/payments")(p) || starts("/vet/payout")(p) },
      { href: "/profile", label: "Profile", icon: Settings, match: starts("/profile") },
    ],
    desktop: [
      { href: "/vet", label: "Today", icon: House, match: (p) => p === "/vet" },
      { href: "/vet/requests", label: "Requests", icon: Inbox, match: starts("/vet/requests") },
      { href: "/cases", label: "My cases", icon: ClipboardList, match: starts("/cases") },
      { href: "/payments", label: "Earnings", icon: Wallet, match: starts("/payments") },
      { href: "/vet/payout", label: "Payout account", icon: Wallet, match: starts("/vet/payout") },
      { href: "/profile", label: "Profile", icon: Settings, match: starts("/profile") },
    ],
  },
  Admin: {
    mobile: [
      { href: "/admin", label: "Overview", icon: LayoutDashboard, match: (p) => p === "/admin" },
      { href: "/admin/users", label: "Users", icon: Users, match: starts("/admin/users") },
      { href: "/admin/vets", label: "Vets", icon: Stethoscope, match: starts("/admin/vets") },
      { href: "/admin/cases", label: "Cases", icon: ClipboardList, match: starts("/admin/cases") },
      { href: "/admin/payments", label: "Payments", icon: Wallet, match: starts("/admin/payments") },
    ],
    desktop: [
      { href: "/admin", label: "Overview", icon: LayoutDashboard, match: (p) => p === "/admin" },
      { href: "/admin/users", label: "Users", icon: Users, match: starts("/admin/users") },
      { href: "/admin/vets", label: "Veterinarians", icon: Stethoscope, match: starts("/admin/vets") },
      { href: "/admin/farms", label: "Farms", icon: Tractor, match: starts("/admin/farms") },
      { href: "/admin/cases", label: "Cases", icon: ClipboardList, match: starts("/admin/cases") },
      { href: "/admin/payments", label: "Payments", icon: Wallet, match: starts("/admin/payments") },
      { href: "/admin/audit", label: "Audit log", icon: ScrollText, match: starts("/admin/audit") },
      { href: "/admin/system", label: "System health", icon: Server, match: starts("/admin/system") },
      { href: "/profile", label: "Profile", icon: Settings, match: starts("/profile") },
    ],
  },
};

export const ActivityIcon = Activity;
