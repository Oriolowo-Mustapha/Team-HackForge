"use client";

import { useQuery } from "@tanstack/react-query";
import { livestockApi } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import { AnimalForm } from "@/components/livestock/animal-form";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState } from "@/components/ui/states";

export function EditAnimal({ id }: { id: string }) {
  const detail = useQuery({ queryKey: qk.animal(id), queryFn: () => livestockApi.animal(id) });
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader back={`/animals/${id}`} title={detail.data ? `Edit ${detail.data.animal.name}` : "Edit animal"} />
      {detail.isPending ? (
        <Skeleton className="h-96 rounded-[28px]" />
      ) : detail.isError ? (
        <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <AnimalForm farmId={detail.data.animal.farmId} animal={detail.data.animal} />
      )}
    </div>
  );
}
