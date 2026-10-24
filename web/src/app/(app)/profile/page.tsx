import type { Metadata } from "next";
import { ProfileScreen } from "./profile-screen";

export const metadata: Metadata = { title: "Profile & settings" };

export default function ProfilePage() {
  return <ProfileScreen />;
}
