"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { toast } from "sonner";
import { sessionApi } from "@/lib/api/endpoints";
import { clearLocalData } from "@/lib/drafts";

/** Signs out on the server (revoking the refresh token), then wipes everything kept on this device. */
export function useSignOut() {
  const client = useQueryClient();
  const [pending, setPending] = useState(false);

  const signOut = useCallback(
    async (everywhere = false) => {
      setPending(true);
      let globallyConfirmed = false;
      try {
        const result = await sessionApi.logout(everywhere);
        globallyConfirmed = result.everywhereSignedOut;
      } catch {
        // Only a completed BFF response can clear HttpOnly cookies. A browser network failure cannot.
        setPending(false);
        toast.error("Sign-out could not finish. Reconnect and try again; this session is still active.");
        return;
      }
      await clearLocalData();
      client.clear();
      // A full page load guarantees no signed-in client state survives sign-out.
      window.location.assign(everywhere && !globallyConfirmed ? "/login?partialSignOut=1" : "/login?signedOut=1");
    },
    [client],
  );

  return { signOut, pending };
}
