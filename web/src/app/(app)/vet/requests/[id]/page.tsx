import { VetRequestScreen } from "@/components/vets/vet-screens";
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <VetRequestScreen id={id} />;
}
