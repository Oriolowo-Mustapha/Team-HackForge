import type { Metadata } from "next";
import { EditAnimal } from "./edit-animal";

export const metadata: Metadata = { title: "Edit animal" };

export default async function EditAnimalPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <EditAnimal id={id} />;
}
