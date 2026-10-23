export type Role = "Farmer" | "Vet" | "Admin";

export function homeFor(role: string | null | undefined): string {
  if (role === "Admin") return "/admin";
  if (role === "Vet") return "/vet";
  return "/home";
}
