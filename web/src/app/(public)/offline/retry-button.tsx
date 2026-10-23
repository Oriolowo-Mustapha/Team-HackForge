"use client";

import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

export function RetryButton() {
  return (
    <Button className="mt-6" icon={<RefreshCw className="size-4" />} onClick={() => window.location.reload()}>
      Try again
    </Button>
  );
}
