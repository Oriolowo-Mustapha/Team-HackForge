"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { authApi } from "@/lib/api/endpoints";
import { PASSWORD_HINT, passwordProblem } from "@/components/auth/auth-frame";
import { Button, ButtonLink } from "@/components/ui/button";
import { Field, PasswordInput } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";

export function ResetForm({ token }: { token: string | null }) {
  const router = useRouter();
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [fieldError, setFieldError] = useState<{ password?: string; confirm?: string }>({});
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  if (!token) {
    return (
      <div className="space-y-4">
        <Notice tone="warning">This reset link is incomplete. Open the link from your email again, or request a new one.</Notice>
        <ButtonLink href="/forgot-password" block size="lg">
          Request a new link
        </ButtonLink>
      </div>
    );
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const problem = passwordProblem(password);
    const next = { password: problem ?? undefined, confirm: confirm !== password ? "The passwords don't match." : undefined };
    setFieldError(next);
    if (next.password || next.confirm) return;
    setPending(true);
    setError(null);
    try {
      await authApi.resetPassword(token!, password);
      router.replace("/login?reset=1");
    } catch (err) {
      setError(err);
      setPending(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      <Field label="New password" hint={PASSWORD_HINT} error={fieldError.password}>
        <PasswordInput autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </Field>
      <Field label="Confirm new password" error={fieldError.confirm}>
        <PasswordInput autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      </Field>
      <InlineError error={error} />
      <Button type="submit" size="lg" block loading={pending}>
        Save new password
      </Button>
    </form>
  );
}
