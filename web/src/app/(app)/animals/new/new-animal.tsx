"use client";

import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { AnimalForm } from "@/components/livestock/animal-form";
import { PageHeader } from "@/components/ui/page-header";

export function NewAnimal({ groupId }: { groupId: string | null }) {
  const { farm } = useActiveFarm();
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader back="/animals" title="Add an animal" subtitle="For animals you track individually. Flocks and herds can be reported on as a group." />
      {farm && <AnimalForm farmId={farm.id} defaultGroupId={groupId} />}
    </div>
  );
}
