import type { Metadata } from "next";
import Link from "next/link";
import { AuthFrame } from "@/components/auth/auth-frame";
import { RegisterForm } from "./register-form";

export const metadata: Metadata = { title: "Create a farm account" };

export default function RegisterPage() {
  return (
    <AuthFrame
      title="Create your farm account"
      subtitle="Free for farmers. You'll set up your farm and first flock or animal next."
      photo="/images/poultry-worker"
      caption="Write it down, say it, or snap it. FarmHand keeps the record."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/login" className="font-semibold text-ink underline decoration-beige decoration-2 underline-offset-4 hover:decoration-ink">
            Sign in
          </Link>
        </>
      }
    >
      <RegisterForm />
    </AuthFrame>
  );
}
