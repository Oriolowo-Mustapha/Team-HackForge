import Link from "next/link";
import { cn } from "@/lib/cn";

export function BrandMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 512 512" className={cn("size-9", className)} aria-hidden="true">
      <rect width="512" height="512" rx="120" fill="#0B0B06" />
      <path d="M128 384C124 236 220 136 386 124C396 290 300 390 128 384Z" fill="#EEEAE1" />
      <g transform="translate(257 254) rotate(-45)">
        <polyline points="-150,0 -46,0 -24,-52 6,58 30,-22 48,0 104,0" fill="none" stroke="#0B0B06" strokeWidth="22" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx="136" cy="0" r="19" fill="#C15935" />
      </g>
    </svg>
  );
}

export function Brand({ href = "/", inverse = false, className }: { href?: string; inverse?: boolean; className?: string }) {
  return (
    <Link href={href} className={cn("inline-flex items-center gap-2.5 rounded-xl", className)} aria-label="FarmHand home">
      <BrandMark className={cn("size-9", inverse && "[&_rect]:fill-[#23211a]")} />
      <span className={cn("text-[19px] font-bold tracking-[-0.03em]", inverse ? "text-cream" : "text-ink")}>FarmHand</span>
    </Link>
  );
}
