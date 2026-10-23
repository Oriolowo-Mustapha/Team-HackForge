"use client";

import { MailCheck } from "lucide-react";
import { useState, type FormEvent } from "react";
import { authApi } from "@/lib/api/endpoints";
import { useSystemInfo } from "@/lib/hooks/use-session";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";

export function ForgotForm() {
  const system = useSystemInfo();
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) {
      setError(new Error("Enter a valid email address."));
      return;
    }
    setPending(true);
    setError(null);
    try {
      const result = await authApi.forgotPassword(email.trim());
      setSent(result.message);
    } catch (err) {
      setError(err);
    } finally {
      setPending(false);
    }
  }

  if (sent) {
    return (
      <div className="space-y-4">
        <Notice tone="success" icon={<MailCheck />} title="Check your email">
          {sent} The link works for 30 minutes.
        </Notice>
        {system.data && !system.data.emailEnabled && (
          <Notice tone="demo" title="Email is not configured on this server">
            No email will arrive. In development the reset link is written to the API server log; otherwise ask an administrator to reset your access.
          </Notice>
        )}
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      <Field label="Email">
        <Input type="email" inputMode="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" />
      </Field>
      <InlineError error={error} />
      <Button type="submit" size="lg" block loading={pending}>
        Send reset link
      </Button>
    </form>
  );
}
