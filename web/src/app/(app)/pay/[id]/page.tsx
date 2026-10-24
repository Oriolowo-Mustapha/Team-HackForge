import { PayScreen } from "@/components/payments/payment-screens";
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PayScreen requestId={id} />;
}
