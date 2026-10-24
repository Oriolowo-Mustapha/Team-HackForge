import { VetRequestsScreen } from "@/components/vets/vet-screens";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ scope?: string }>;
}) {
  const { scope } = await searchParams;
  return (
    <VetRequestsScreen
      initialScope={scope === "assigned" ? "assigned" : "open"}
    />
  );
}
