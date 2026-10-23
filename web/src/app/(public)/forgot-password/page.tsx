import type { Metadata } from "next";
import Link from "next/link";
import { AuthFrame } from "@/components/auth/auth-frame";
import { ForgotForm } from "./forgot-form";

export const metadata: Metadata = { title: "Reset your password" };

export default function ForgotPasswordPage() {
  return (
    <AuthFrame
      title="Forgot your password?"
      subtitle="Enter the email you signed up with and we'll send a link to choose a new one."
      photo="/images/hens-closeup"
      footer={
        <Link href="/login" className="font-semibold text-ink underline decoration-beige decoration-2 underline-offset-4 hover:decoration-ink">
          Back to sign in
        </Link>
      }
    >
      <ForgotForm />
    </AuthFrame>
  );
}
