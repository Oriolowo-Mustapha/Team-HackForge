import type { Metadata } from "next";
import { AnimalsScreen } from "./animals-screen";

export const metadata: Metadata = { title: "Animals & flocks" };

export default function AnimalsPage() {
  return <AnimalsScreen />;
}
