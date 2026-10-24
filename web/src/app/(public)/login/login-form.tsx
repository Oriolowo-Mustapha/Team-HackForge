"use client";

import { useQueryClient } from "@tanstack/react-query";
import { FlaskConical } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api/client";
import { sessionApi } from "@/lib/api/endpoints";
import { useSystemInfo } from "@/lib/hooks/use-session";
import { clearLocalData } from "@/lib/drafts";
import { homeFor } from "@/lib/roles";
import { Button } from "@/components/ui/button";
import { Field, Input, PasswordInput } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";

const notices = {
  expired: { tone: "warning", text: "Your session ended. Sign in again. Saved drafts are private to the account that created them." },
  signedOut: { tone: "info", text: "You've signed out. Saved drafts are private to the account that created them." },
  partialSignOut: { tone: "warning", text: "Signed out on this device. Sign-out on other devices could not be confirmed. Sign in and try Sign out everywhere again." },
  reset: { tone: "success", text: "Your password was reset. Sign in with your new password." },
  changed: { tone: "success", text: "Password changed. Sign in with your new password." },
} as const;

const DEMO_ACCOUNTS = [
  { email: "farmer@farmhand.ai", label: "Farmer" },
  { email: "vet@farmhand.ai", label: "Vet" },
  { email: "admin@farmhand.ai", label: "Admin" },
];

export function LoginForm({ next, notice }: { next: string | null; notice: keyof typeof notices | null }) {
  const router = useRouter();
  const client = useQueryClient();
  const system = useSystemInfo();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    if (!email.trim() || !password) {
      setError(new Error("Enter your email and password."));
      return;
    }
    setPending(true);
    setError(null);
    try {
      const result = await sessionApi.login(email.trim(), password);
      await client.cancelQueries();
      client.clear();
      await clearLocalData();
      if (result.mustChangePassword) router.replace("/change-password");
      else router.replace(next ?? homeFor(result.role));
    } catch (err) {
      setError(err instanceof ApiError && err.status === 401 ? new Error("That email and password don't match an active account.") : err);
      setPending(false);
    }
  }

  const n = notice ? notices[notice] : null;

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      {n && <Notice tone={n.tone}>{n.text}</Notice>}
      <Field label="Email">
        <Input type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" required />
      </Field>
      <Field label="Password">
        <PasswordInput autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
      </Field>
      <div className="-mt-2 flex justify-end">
        <Link href="/forgot-password" className="rounded-lg text-sm font-semibold text-muted hover:text-ink">
          Forgot password?
        </Link>
      </div>
      <InlineError error={error} />
      <Button type="submit" size="lg" block loading={pending}>
        Sign in
      </Button>

      {system.data?.demoMode && (
        <div className="rounded-[22px] bg-olive-soft p-4 text-olive-ink">
          <p className="flex items-center gap-2 text-sm font-semibold">
            <FlaskConical className="size-4" aria-hidden /> Demo server
          </p>
          <p className="mt-1 text-[13px] leading-relaxed">
            Sample accounts are loaded with made-up farms and cases. Pick one to fill the email; the shared demo password is in the demo guide.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            {DEMO_ACCOUNTS.map((account) => (
              <button
                key={account.email}
                type="button"
                onClick={() => setEmail(account.email)}
                className="rounded-full bg-paper px-3.5 py-2 text-[13px] font-semibold text-ink ring-1 ring-line hover:bg-white"
              >
                {account.label}
              </button>
            ))}
          </div>
        </div>
      )}
    </form>
  );
}
