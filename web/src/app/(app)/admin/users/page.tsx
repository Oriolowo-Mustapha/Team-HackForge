import type { Metadata } from "next";
import { AdminUsersScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Users" };
export default function Page() {
  return <AdminUsersScreen />;
}
