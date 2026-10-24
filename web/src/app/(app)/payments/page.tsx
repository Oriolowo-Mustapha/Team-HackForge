import type { Metadata } from "next";
import { PaymentsScreen } from "@/components/payments/payment-screens";
export const metadata: Metadata = { title: "Payments" };
export default function Page() {
  return <PaymentsScreen />;
}
