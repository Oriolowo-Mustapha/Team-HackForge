import type { Metadata } from "next";
import Link from "next/link";
import { AuthFrame } from "@/components/auth/auth-frame";
import { ResetForm } from "./reset-form";

export const metadata: Metadata = { title: "Choose a new password" };

export default async function ResetPasswordPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const token = typeof params.token === "string" ? params.token : null;
  return (
    <AuthFrame
      title="Choose a new password"
      subtitle="Signing in with the new password ends all your other sessions."
      photo="/images/hens-closeup"
      footer={
        <Link href="/login" className="font-semibold text-ink underline decoration-beige decoration-2 underline-offset-4 hover:decoration-ink">
          Back to sign in
        </Link>
      }
    >
      <ResetForm token={token} />
    </AuthFrame>
  );
}
