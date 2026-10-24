import type { Metadata } from "next";
import { ProfileScreen } from "@/components/account/profile-screen";
export const metadata: Metadata = { title: "Profile" };
export default function Page() {
  return <ProfileScreen />;
}
