import type { Metadata } from "next";
import { NewAnimal } from "./new-animal";

export const metadata: Metadata = { title: "Add an animal" };

export default async function NewAnimalPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  return <NewAnimal groupId={typeof params.group === "string" ? params.group : null} />;
}
