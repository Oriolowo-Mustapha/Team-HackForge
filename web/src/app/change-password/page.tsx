import type { Metadata } from "next";
import { AuthFrame } from "@/components/auth/auth-frame";
import { ChangePasswordForm } from "./change-password-form";

export const metadata: Metadata = { title: "Change your password" };

export default function ChangePasswordPage() {
  return (
    <AuthFrame title="Change your password" subtitle="You'll sign in again with the new password. Other devices are signed out." photo="/images/vet-cattle" caption="Your account, your animals' records. Keep them safe.">
      <ChangePasswordForm />
    </AuthFrame>
  );
}
