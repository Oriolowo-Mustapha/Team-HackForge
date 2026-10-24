"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { KeyRound, LogOut } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { authApi } from "@/lib/api/endpoints";
import { firstName, formatDate } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { qk } from "@/lib/queries";
import { Avatar } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { ErrorState, InlineError } from "@/components/ui/states";
import { CurrentFarmSettings } from "@/components/account/profile-screen";

/** Account details, quick settings and sign out. Open to every role; the shell gates access. */
export function ProfileScreen() {
  const { user, isError, error, refetch } = useSession();

  if (isError && !user) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!user) return null; // The shell redirects anonymous visitors to /login.

  const roleLabel = user.role === "Vet" ? "Veterinarian" : user.role;

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <header className="flex items-center gap-4">
        <Avatar name={user.fullName ?? user.email} className="size-16 text-lg" tone={user.role === "Farmer" ? "terracotta" : "beige"} />
        <div className="min-w-0">
          <h1 className="truncate text-[26px] font-semibold leading-tight tracking-[-0.025em]">{user.fullName ?? firstName(user.email)}</h1>
          <p className="mt-0.5 flex flex-wrap items-center gap-2">
            <Badge tone={user.role === "Vet" ? "healthy" : "neutral"}>{roleLabel}</Badge>
            <span className="truncate text-[14px] text-muted">{user.email}</span>
          </p>
        </div>
      </header>

      <ProfileForm fullName={user.fullName ?? ""} phoneNumber={user.phoneNumber ?? ""} />
      <CurrentFarmSettings />

      {user.role === "Farmer" && user.farms.length > 0 && (
        <Card>
          <CardTitle>Your farms</CardTitle>
          <ul className="mt-2 space-y-2">
            {user.farms.map((f) => (
              <li key={f.id} className="rounded-2xl bg-paper px-3.5 py-2.5 ring-1 ring-line/60">
                <span className="block truncate text-[15px] font-semibold">{f.displayName}</span>
                <span className="block truncate text-[13px] text-muted">{f.location || "No location set"}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {user.role === "Vet" && user.vetProfile && (
        <Card>
          <CardTitle>Veterinary profile</CardTitle>
          <dl className="mt-2 space-y-2 text-[15px]">
            <p>
              <span className="text-muted">Name:</span> <span className="font-semibold">{user.vetProfile.name}</span>
            </p>
            <p>
              <span className="text-muted">Specialization:</span> <span className="font-semibold">{user.vetProfile.specialization}</span>
            </p>
            <p>
              <span className="text-muted">Contact:</span> <span className="font-semibold">{user.vetProfile.contact}</span>
            </p>
          </dl>
        </Card>
      )}

      <Card>
        <CardTitle>Security</CardTitle>
        <div className="mt-3 flex flex-col gap-2 sm:flex-row">
          <ButtonLink href="/change-password" variant="secondary" icon={<KeyRound className="size-4" />}>
            Change password
          </ButtonLink>
          <SignOutButton everywhere={false} label="Sign out" />
          <SignOutButton everywhere label="Sign out everywhere" />
        </div>
        <p className="mt-3 text-[13px] text-muted">Member since {formatDate(user.createdAt)}.</p>
      </Card>
    </div>
  );
}

function ProfileForm({ fullName, phoneNumber }: { fullName: string; phoneNumber: string }) {
  const client = useQueryClient();
  const [name, setName] = useState(fullName);
  const [phone, setPhone] = useState(phoneNumber);

  const save = useMutation({
    mutationFn: () => authApi.updateProfile({ fullName: name.trim() || null, phoneNumber: phone.trim() || null }),
    onSuccess: async () => {
      toast.success("Profile updated.");
      await client.invalidateQueries({ queryKey: qk.session });
    },
  });

  const dirty = name !== fullName || phone !== phoneNumber;

  return (
    <Card>
      <CardTitle>Account details</CardTitle>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate();
        }}
        className="mt-3 space-y-4"
      >
        <Field label="Full name">
          <Input value={name} onChange={(e) => setName(e.target.value)} maxLength={120} autoComplete="name" />
        </Field>
        <Field label="Phone number" optional hint="Shown only to vets you accept or who accept your request.">
          <Input value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={20} inputMode="tel" autoComplete="tel" />
        </Field>
        <InlineError error={save.error} />
        <Button type="submit" variant="primary" loading={save.isPending} disabled={!dirty}>
          Save changes
        </Button>
      </form>
    </Card>
  );
}

function SignOutButton({ label, everywhere }: { label: string; everywhere?: boolean }) {
  const { signOut, pending } = useSignOut();
  return (
    <Button variant={everywhere ? "ghost" : "danger"} loading={pending} icon={everywhere ? undefined : <LogOut className="size-4" aria-hidden />} onClick={() => void signOut(everywhere)}>
      {pending ? "Signing out…" : label}
    </Button>
  );
}
