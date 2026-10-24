"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Stethoscope } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api/client";
import { sessionApi } from "@/lib/api/endpoints";
import { clearLocalData } from "@/lib/drafts";
import { PASSWORD_HINT, passwordProblem } from "@/components/auth/auth-frame";
import { Button } from "@/components/ui/button";
import { Field, Input, PasswordInput } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";

type Errors = Partial<Record<"fullName" | "email" | "phone" | "password" | "confirm", string>>;

export function RegisterForm() {
  const router = useRouter();
  const client = useQueryClient();
  const [form, setForm] = useState({ fullName: "", email: "", phone: "", password: "", confirm: "" });
  const [errors, setErrors] = useState<Errors>({});
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [key]: e.target.value }));

  function validate(): Errors {
    const next: Errors = {};
    if (form.fullName.trim().length < 2) next.fullName = "Enter your name.";
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) next.email = "Enter a valid email address.";
    if (form.phone && !/^\+?[\d\s-]{7,20}$/.test(form.phone.trim())) next.phone = "Use digits only, for example +234 803 000 0000.";
    const pw = passwordProblem(form.password);
    if (pw) next.password = pw;
    if (form.confirm !== form.password) next.confirm = "The passwords don't match.";
    return next;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    const found = validate();
    setErrors(found);
    if (Object.keys(found).length) return;
    setPending(true);
    setError(null);
    try {
      await sessionApi.register({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        password: form.password,
        phoneNumber: form.phone.trim() || undefined,
      });
      await client.cancelQueries();
      client.clear();
      await clearLocalData();
      router.replace("/setup");
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) setErrors({ email: "An account with this email already exists. Try signing in." });
      else setError(err);
      setPending(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      <Field label="Full name" error={errors.fullName}>
        <Input autoComplete="name" value={form.fullName} onChange={set("fullName")} placeholder="Funmilayo Adeyemi" />
      </Field>
      <Field label="Email" error={errors.email}>
        <Input type="email" inputMode="email" autoComplete="email" value={form.email} onChange={set("email")} placeholder="you@example.com" />
      </Field>
      <Field label="Phone number" optional hint="Shown only to a vet who accepts your request." error={errors.phone}>
        <Input type="tel" inputMode="tel" autoComplete="tel" value={form.phone} onChange={set("phone")} placeholder="+234 803 000 0000" />
      </Field>
      <Field label="Password" hint={PASSWORD_HINT} error={errors.password}>
        <PasswordInput autoComplete="new-password" value={form.password} onChange={set("password")} />
      </Field>
      <Field label="Confirm password" error={errors.confirm}>
        <PasswordInput autoComplete="new-password" value={form.confirm} onChange={set("confirm")} />
      </Field>
      <InlineError error={error} />
      <Button type="submit" size="lg" block loading={pending}>
        Create account
      </Button>
      <Notice icon={<Stethoscope />} title="Are you a veterinarian?">
        Vet accounts are verified and created by the FarmHand team so farmers know who they&apos;re talking to. Contact your FarmHand administrator to be onboarded.
      </Notice>
    </form>
  );
}
