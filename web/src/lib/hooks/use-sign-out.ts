"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { sessionApi } from "@/lib/api/endpoints";
import { clearLocalData } from "@/lib/drafts";

/** Signs out on the server (revoking the refresh token), then wipes everything kept on this device. */
export function useSignOut() {
  const client = useQueryClient();
  const [pending, setPending] = useState(false);

  const signOut = useCallback(
    async (everywhere = false) => {
      setPending(true);
      try {
        await sessionApi.logout(everywhere);
      } catch {
        // The BFF clears cookies even when the API is unreachable; local data is wiped regardless.
      }
      await clearLocalData();
      client.clear();
      // A full page load guarantees no signed-in client state survives sign-out.
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination
      window.location.assign("/login?signedOut=1");
    },
    [client],
  );

  return { signOut, pending };
}
