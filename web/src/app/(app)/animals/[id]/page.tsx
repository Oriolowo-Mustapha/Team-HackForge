import type { Metadata } from "next";
import { AnimalDetail } from "./animal-detail";

export const metadata: Metadata = { title: "Animal" };

export default async function AnimalPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AnimalDetail id={id} />;
}
