import type { Metadata } from "next";
import { CasesScreen } from "./cases-screen";

export const metadata: Metadata = { title: "Cases" };

export default function CasesPage() {
  return <CasesScreen />;
}
