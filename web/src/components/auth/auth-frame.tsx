import Image from "next/image";
import type { ReactNode } from "react";
import { Brand } from "@/components/shell/brand";

/** Split layout for sign-in and account pages: editorial photo panel plus a focused form column. */
export function AuthFrame({
  title,
  subtitle,
  children,
  footer,
  photo = "/images/farmer-goats",
  caption = "Healthier herds start with a quick daily check.",
}: {
  title: string;
  subtitle?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  photo?: string;
  caption?: string;
}) {
  return (
    <div className="min-h-dvh bg-cream lg:grid lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)]">
      <div className="relative h-[210px] overflow-hidden rounded-b-[36px] sm:h-[260px] lg:sticky lg:top-3 lg:m-3 lg:h-[calc(100dvh-24px)] lg:rounded-[36px]">
        <Image src={`${photo}.webp`} alt="" fill priority sizes="(min-width: 1024px) 50vw, 100vw" className="object-cover" />
        <div className="absolute inset-0 bg-gradient-to-b from-ink/55 via-ink/5 to-ink/75" aria-hidden />
        <div className="absolute left-5 top-[max(20px,env(safe-area-inset-top))] sm:left-8 lg:left-10 lg:top-10">
          <Brand inverse />
        </div>
        <p className="absolute inset-x-10 bottom-10 hidden max-w-md text-[34px] font-semibold leading-[1.1] tracking-[-0.03em] text-cream text-balance lg:block">
          {caption}
        </p>
      </div>
      <main className="px-5 pb-12 pt-8 sm:px-8 lg:flex lg:items-center lg:justify-center lg:py-16">
        <div className="mx-auto w-full max-w-[420px]">
          <h1 className="text-[30px] font-semibold leading-tight tracking-[-0.03em] text-balance">{title}</h1>
          {subtitle && <p className="mt-2 text-[15px] leading-relaxed text-muted text-pretty">{subtitle}</p>}
          <div className="mt-7">{children}</div>
          {footer && <div className="mt-8 text-center text-[15px] text-muted">{footer}</div>}
        </div>
      </main>
    </div>
  );
}

/** Only same-site relative paths are accepted as post-login destinations (prevents open redirects). */
export function safeNext(value: string | string[] | undefined): string | null {
  const next = Array.isArray(value) ? value[0] : value;
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return null;
  if (next.startsWith("/login") || next.startsWith("/register") || next.startsWith("/api")) return null;
  return next;
}

export const PASSWORD_HINT = "At least 8 characters, with a letter and a number.";

export function passwordProblem(password: string): string | null {
  if (password.length < 8) return "Use at least 8 characters.";
  if (new TextEncoder().encode(password).length > 72) return "That password is too long.";
  if (!/[a-zA-Z]/.test(password) || !/\d/.test(password)) return "Include at least one letter and one number.";
  return null;
}
