"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { authApi, farmsApi } from "@/lib/api/endpoints";
import type { SessionUser } from "@/lib/api/schemas";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { useSession } from "@/lib/hooks/use-session";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { qk } from "@/lib/queries";
import { Button, ButtonLink } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Panel } from "@/components/ui/resource-panel";
import { InlineError } from "@/components/ui/states";

function ProfileForm({ user }: { user: SessionUser }) {
  const client = useQueryClient();
  const [name, setName] = useState(user.fullName ?? "");
  const [phone, setPhone] = useState(user.phoneNumber ?? "");
  const save = useMutation({
    mutationFn: () =>
      authApi.updateProfile({
        fullName: name.trim() || null,
        phoneNumber: phone.trim() || null,
      }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: qk.session });
      toast.success("Profile saved");
    },
  });
  return (
    <form
      className="space-y-4"
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <p className="text-sm text-muted">
        {user.email} · {user.role}
      </p>
      <Field label="Full name">
        <Input
          value={name}
          maxLength={150}
          autoComplete="name"
          onChange={(e) => setName(e.target.value)}
        />
      </Field>
      <Field label="Phone number" optional>
        <Input
          type="tel"
          value={phone}
          maxLength={30}
          autoComplete="tel"
          onChange={(e) => setPhone(e.target.value)}
        />
      </Field>
      <InlineError error={save.error} />
      <Button type="submit" loading={save.isPending}>
        Save profile
      </Button>
    </form>
  );
}

function FarmForm({
  id,
  name: initialName,
  location: initialLocation,
}: {
  id: string;
  name: string;
  location: string;
}) {
  const client = useQueryClient();
  const [name, setName] = useState(initialName);
  const [location, setLocation] = useState(initialLocation);
  const save = useMutation({
    mutationFn: () => farmsApi.update(id, name.trim(), location.trim()),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.session }),
        client.invalidateQueries({ queryKey: qk.farms }),
        client.invalidateQueries({ queryKey: qk.overview(id) }),
      ]);
      toast.success("Farm details saved");
    },
  });
  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <Field label="Farm name">
        <Input
          required
          minLength={2}
          maxLength={200}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </Field>
      <Field label="Location">
        <Input
          required
          maxLength={300}
          value={location}
          onChange={(e) => setLocation(e.target.value)}
        />
      </Field>
      <InlineError error={save.error} />
      <Button type="submit" loading={save.isPending}>
        Save farm
      </Button>
    </form>
  );
}

export function ProfileScreen() {
  const { user } = useSession();
  const { farm } = useActiveFarm();
  const { signOut, pending } = useSignOut();
  if (!user) return null;
  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <PageHeader
        title="Profile & settings"
        subtitle="Keep your contact and farm details current."
      />
      <Panel title="Your account">
        <ProfileForm key={user.userId} user={user} />
      </Panel>
      {user.role === "Farmer" && farm && (
        <Panel title="Current farm">
          <FarmForm
            key={farm.id}
            id={farm.id}
            name={farm.displayName}
            location={farm.location}
          />
          <ButtonLink href="/setup?another=1" variant="ghost" className="mt-3">
            Add another farm
          </ButtonLink>
        </Panel>
      )}
      <Panel title="Account security">
        <div className="flex flex-wrap gap-3">
          <ButtonLink href="/change-password" variant="secondary">
            Change password
          </ButtonLink>
          <Button
            variant="ghost"
            onClick={() => void signOut()}
            loading={pending}
          >
            Sign out
          </Button>
        </div>
      </Panel>
    </div>
  );
}
