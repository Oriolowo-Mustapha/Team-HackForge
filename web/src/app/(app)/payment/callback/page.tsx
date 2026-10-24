import type { Metadata } from "next";
import { Suspense } from "react";
import { FullScreenLoader } from "@/components/shell/app-shell";
import { CallbackScreen } from "./callback-screen";

export const metadata: Metadata = { title: "Payment status" };

export default function PaymentCallbackPage() {
  return (
    <Suspense fallback={<FullScreenLoader label="Checking your payment" />}>
      <CallbackScreen />
    </Suspense>
  );
}
