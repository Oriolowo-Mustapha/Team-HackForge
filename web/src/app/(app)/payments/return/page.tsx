import { PaymentReturnScreen } from "@/components/payments/payment-screens";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ tx_ref?: string }>;
}) {
  const { tx_ref } = await searchParams;
  return (
    <PaymentReturnScreen
      reference={
        typeof tx_ref === "string" && tx_ref.length <= 250 ? tx_ref : null
      }
    />
  );
}
