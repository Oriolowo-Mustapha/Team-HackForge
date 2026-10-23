import type { Metadata } from "next";
import { AuthFrame, safeNext } from "@/components/auth/auth-frame";
import { LoginForm } from "./login-form";
import Link from "next/link";

export const metadata: Metadata = { title: "Sign in" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const notice = params.expired ? "expired" : params.signedOut ? "signedOut" : params.reset ? "reset" : params.changed ? "changed" : null;
  return (
    <AuthFrame
      title="Welcome back"
      subtitle="Sign in to check on your animals, cases and vet visits."
      footer={
        <>
          New to FarmHand?{" "}
          <Link href="/register" className="font-semibold text-ink underline decoration-beige decoration-2 underline-offset-4 hover:decoration-ink">
            Create a farm account
          </Link>
        </>
      }
    >
      <LoginForm next={safeNext(params.next)} notice={notice} />
    </AuthFrame>
  );
}
