"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { authApi } from "@/lib/api/endpoints";
import { useSession } from "@/lib/hooks/use-session";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { homeFor } from "@/lib/roles";
import { PASSWORD_HINT, passwordProblem } from "@/components/auth/auth-frame";
import { Button, ButtonLink } from "@/components/ui/button";
import { Field, PasswordInput } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";
import { Spinner } from "@/components/ui/spinner";
import { sessionApi } from "@/lib/api/endpoints";
import { clearLocalData } from "@/lib/drafts";

export function ChangePasswordForm() {
  const router = useRouter();
  const { user, status } = useSession();
  const { signOut, pending: signingOut } = useSignOut();
  const [current, setCurrent] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [fieldError, setFieldError] = useState<{ password?: string; confirm?: string; current?: string }>({});
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    if (status === "anonymous") router.replace("/login?next=/change-password");
  }, [status, router]);

  if (!user) {
    return (
      <div className="flex justify-center py-10">
        <Spinner className="size-6 text-muted" label="Loading" />
      </div>
    );
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const next = {
      current: current ? undefined : "Enter your current password.",
      password: passwordProblem(password) ?? (password === current ? "Choose a password different from the current one." : undefined),
      confirm: confirm !== password ? "The passwords don't match." : undefined,
    };
    setFieldError(next);
    if (next.current || next.password || next.confirm) return;
    setPending(true);
    setError(null);
    try {
      await authApi.changePassword(current, password);
      // The API revokes every session after a password change; clear this device and sign in again.
      await sessionApi.logout().catch(() => undefined);
      await clearLocalData();
      // A full page load guarantees no signed-in client state survives sign-out.
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination
      window.location.assign("/login?changed=1");
    } catch (err) {
      setError(err);
      setPending(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      {user.mustChangePassword && (
        <Notice tone="warning" title="A new password is required">
          Your account was set up with a temporary password. Choose your own before continuing.
        </Notice>
      )}
      <Field label={user.mustChangePassword ? "Temporary password" : "Current password"} error={fieldError.current}>
        <PasswordInput autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
      </Field>
      <Field label="New password" hint={PASSWORD_HINT} error={fieldError.password}>
        <PasswordInput autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </Field>
      <Field label="Confirm new password" error={fieldError.confirm}>
        <PasswordInput autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      </Field>
      <InlineError error={error} />
      <Button type="submit" size="lg" block loading={pending}>
        Change password
      </Button>
      {user.mustChangePassword ? (
        <Button variant="ghost" block onClick={() => void signOut()} loading={signingOut}>
          Sign out instead
        </Button>
      ) : (
        <ButtonLink href={homeFor(user.role)} variant="ghost" block>
          Cancel
        </ButtonLink>
      )}
    </form>
  );
}
