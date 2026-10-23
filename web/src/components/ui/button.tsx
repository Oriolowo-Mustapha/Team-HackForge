import Link from "next/link";
import { forwardRef, type ButtonHTMLAttributes, type ComponentProps, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Spinner } from "./spinner";

type Variant = "primary" | "accent" | "secondary" | "ghost" | "danger" | "subtle" | "inverse";
type Size = "sm" | "md" | "lg";

const variants: Record<Variant, string> = {
  primary: "bg-ink text-cream hover:bg-ink-2 active:bg-ink-3 shadow-soft",
  accent: "bg-terracotta-ink text-white hover:bg-[#953f23] active:bg-[#83371f] shadow-soft",
  secondary: "bg-paper text-ink border border-line hover:bg-sand active:bg-[#ece6da]",
  ghost: "bg-transparent text-ink hover:bg-sand active:bg-[#ece6da]",
  subtle: "bg-sand text-ink hover:bg-[#ece6da] active:bg-[#e4ddcf]",
  danger: "bg-red-ink text-white hover:bg-[#7a2316] active:bg-[#6a1e12]",
  inverse: "bg-cream text-ink hover:bg-white active:bg-sand",
};

const sizes: Record<Size, string> = {
  sm: "h-10 px-4 text-sm gap-1.5 rounded-[14px]",
  md: "h-12 px-5 text-[15px] gap-2 rounded-2xl",
  lg: "h-14 px-6 text-base gap-2.5 rounded-[18px]",
};

const base =
  "inline-flex items-center justify-center font-semibold tracking-[-0.01em] whitespace-nowrap transition-[background-color,transform,box-shadow] duration-150 active:scale-[0.985] disabled:pointer-events-none disabled:opacity-50 select-none";

export function buttonClasses({ variant = "primary", size = "md", block = false, className }: { variant?: Variant; size?: Size; block?: boolean; className?: string } = {}) {
  return cn(base, variants[variant], sizes[size], block && "w-full", className);
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  size?: Size;
  block?: boolean;
  loading?: boolean;
  icon?: ReactNode;
  trailingIcon?: ReactNode;
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant, size, block, loading, icon, trailingIcon, className, children, disabled, type = "button", ...props },
  ref,
) {
  return (
    <button ref={ref} type={type} className={buttonClasses({ variant, size, block, className })} disabled={disabled || loading} aria-busy={loading || undefined} {...props}>
      {loading ? <Spinner className="size-4" /> : icon}
      {children}
      {!loading && trailingIcon}
    </button>
  );
});

type ButtonLinkProps = ComponentProps<typeof Link> & { variant?: Variant; size?: Size; block?: boolean; icon?: ReactNode; trailingIcon?: ReactNode };

export function ButtonLink({ variant, size, block, icon, trailingIcon, className, children, ...props }: ButtonLinkProps) {
  return (
    <Link className={buttonClasses({ variant, size, block, className })} {...props}>
      {icon}
      {children}
      {trailingIcon}
    </Link>
  );
}

export const IconButton = forwardRef<HTMLButtonElement, ButtonHTMLAttributes<HTMLButtonElement> & { label: string; tone?: "paper" | "ghost" | "dark" }>(
  function IconButton({ label, tone = "paper", className, children, type = "button", ...props }, ref) {
    return (
      <button
        ref={ref}
        type={type}
        aria-label={label}
        title={label}
        className={cn(
          "relative inline-flex size-11 shrink-0 items-center justify-center rounded-2xl transition-colors active:scale-95 disabled:opacity-50",
          tone === "paper" && "bg-paper text-ink shadow-soft hover:bg-white",
          tone === "ghost" && "text-ink hover:bg-sand",
          tone === "dark" && "bg-ink text-cream hover:bg-ink-2",
          className,
        )}
        {...props}
      >
        {children}
      </button>
    );
  },
);
