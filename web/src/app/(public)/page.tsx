import { ArrowRight, AudioLines, Camera, ClipboardList, ListChecks, ShieldCheck, Smartphone, Stethoscope, Type } from "lucide-react";
import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { Brand } from "@/components/shell/brand";
import { ButtonLink } from "@/components/ui/button";

export const metadata: Metadata = { title: { absolute: "FarmHand — livestock health, handled" } };

const steps = [
  {
    icon: Camera,
    title: "Report it your way",
    body: "Type a note, record a voice message in your own words, or take a photo. Drafts stay on your phone if the signal drops.",
  },
  {
    icon: ListChecks,
    title: "Review the triage",
    body: "FarmHand pulls out symptoms and suggests how urgent it looks. You check every detail before anything is saved.",
  },
  {
    icon: ClipboardList,
    title: "Keep a case file",
    body: "Actions, follow-ups and prevention notes live on one timeline — per animal or for the whole flock.",
  },
  {
    icon: Stethoscope,
    title: "Bring in a vet",
    body: "Request a remote review or a farm visit. The vet's fee is shown before you pay, and the treatment plan lands in the case.",
  },
];

export default function WelcomePage() {
  return (
    <div className="min-h-dvh bg-cream">
      <header className="mx-auto flex max-w-[1200px] items-center justify-between gap-4 px-5 pt-[max(16px,env(safe-area-inset-top))] sm:px-8">
        <Brand />
        <nav aria-label="Account" className="flex items-center gap-1 sm:gap-2">
          <Link href="/login" className="rounded-xl px-3 py-2.5 text-[15px] font-semibold text-ink hover:bg-paper">
            Sign in
          </Link>
          <ButtonLink href="/register" size="sm" className="hidden sm:inline-flex">
            Get started
          </ButtonLink>
        </nav>
      </header>

      <main>
        <section className="mx-auto grid max-w-[1200px] items-center gap-10 px-5 pb-14 pt-10 sm:px-8 lg:grid-cols-[1.05fr_1fr] lg:gap-14 lg:pb-24 lg:pt-16">
          <div className="animate-fade-up">
            <p className="inline-flex items-center gap-2 rounded-full bg-paper px-3.5 py-1.5 text-[13px] font-semibold text-muted ring-1 ring-line">
              <span className="size-1.5 rounded-full bg-terracotta" aria-hidden />
              For poultry, goat, sheep and cattle farmers
            </p>
            <h1 className="mt-5 text-[44px] font-semibold leading-[1.02] tracking-[-0.045em] text-balance sm:text-[60px] lg:text-[72px]">
              Spot trouble early. Act with a vet, not a guess.
            </h1>
            <p className="mt-5 max-w-[34rem] text-[17px] leading-relaxed text-muted text-pretty">
              FarmHand turns a quick voice note, photo or message about your animals into a clear health record, an honest urgency check and — when it matters — a vet who can help.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <ButtonLink href="/register" size="lg" trailingIcon={<ArrowRight className="size-5" />}>
                Create a free farm account
              </ButtonLink>
              <ButtonLink href="/login" size="lg" variant="secondary">
                I already have an account
              </ButtonLink>
            </div>
          </div>

          <div className="relative">
            <div className="relative aspect-[4/5] overflow-hidden rounded-[40px] shadow-lift sm:aspect-[5/4] lg:aspect-[4/5]">
              <Image src="/images/hens-closeup.webp" alt="Hens in a poultry house" fill priority sizes="(min-width: 1024px) 46vw, 100vw" className="object-cover" />
              <div className="absolute inset-0 bg-gradient-to-t from-ink/60 via-transparent to-transparent" aria-hidden />
            </div>
            <figure className="absolute inset-x-4 bottom-4 rounded-[28px] bg-paper/95 p-4 shadow-lift backdrop-blur sm:inset-x-auto sm:bottom-6 sm:left-6 sm:w-[320px]" aria-label="Example of a reviewed report">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-semibold uppercase tracking-[0.08em] text-faint">Example report</span>
                <span className="inline-flex h-6 items-center gap-1 rounded-full bg-terracotta-soft px-2.5 text-xs font-semibold text-terracotta-deep">Medium · watch closely</span>
              </div>
              <p className="mt-2 text-[15px] font-semibold leading-snug">Layer house B — three birds lethargic, eating less since Monday.</p>
              <div className="mt-3 flex flex-wrap gap-1.5 text-xs font-semibold text-muted">
                {["Lethargy", "Reduced feed intake", "Ruffled feathers"].map((s) => (
                  <span key={s} className="rounded-full bg-sand px-2.5 py-1">
                    {s}
                  </span>
                ))}
              </div>
              <figcaption className="mt-3 text-xs leading-relaxed text-faint">Triage suggestions are not a diagnosis. You confirm every report, and a vet makes the call.</figcaption>
            </figure>
          </div>
        </section>

        <section className="bg-ink py-16 text-cream lg:py-24" aria-labelledby="how">
          <div className="mx-auto max-w-[1200px] px-5 sm:px-8">
            <h2 id="how" className="max-w-2xl text-[32px] font-semibold leading-[1.1] tracking-[-0.035em] text-balance sm:text-[44px]">
              From “something&apos;s not right” to a plan, in four steps.
            </h2>
            <ol className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {steps.map((step, i) => (
                <li key={step.title} className="rounded-[28px] bg-cream/6 p-6 ring-1 ring-cream/10">
                  <div className="flex items-center justify-between">
                    <span className="flex size-12 items-center justify-center rounded-2xl bg-terracotta text-white">
                      <step.icon className="size-6" aria-hidden />
                    </span>
                    <span className="text-sm font-semibold text-cream/40 tabular">0{i + 1}</span>
                  </div>
                  <h3 className="mt-6 text-lg font-semibold">{step.title}</h3>
                  <p className="mt-2 text-[15px] leading-relaxed text-cream/70">{step.body}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section className="mx-auto grid max-w-[1200px] gap-6 px-5 py-16 sm:px-8 lg:grid-cols-2 lg:py-24" aria-labelledby="honest">
          <div className="relative min-h-[320px] overflow-hidden rounded-[36px]">
            <Image src="/images/vet-cattle.webp" alt="A veterinarian examining cattle" fill sizes="(min-width: 1024px) 50vw, 100vw" className="object-cover" />
          </div>
          <div className="flex flex-col justify-center rounded-[36px] bg-paper p-7 ring-1 ring-line/60 sm:p-10">
            <h2 id="honest" className="text-[30px] font-semibold leading-[1.1] tracking-[-0.03em] text-balance sm:text-[38px]">
              Honest about what it knows.
            </h2>
            <ul className="mt-6 space-y-4 text-[15px] leading-relaxed text-muted">
              <li className="flex gap-3">
                <ShieldCheck className="mt-0.5 size-5 shrink-0 text-green-ink" aria-hidden />
                <span>
                  <strong className="text-ink">Triage, not diagnosis.</strong> FarmHand suggests how urgent a report looks and what to do next. Only a vet can diagnose.
                </span>
              </li>
              <li className="flex gap-3">
                <ListChecks className="mt-0.5 size-5 shrink-0 text-green-ink" aria-hidden />
                <span>
                  <strong className="text-ink">Every result says where it came from</strong> — an AI model, a transparent rule-based check when AI is unavailable, or your own entry.
                </span>
              </li>
              <li className="flex gap-3">
                <Stethoscope className="mt-0.5 size-5 shrink-0 text-green-ink" aria-hidden />
                <span>
                  <strong className="text-ink">Your farm stays yours.</strong> Vets see your contact details only after they accept your request.
                </span>
              </li>
            </ul>
          </div>
        </section>

        <section className="mx-auto max-w-[1200px] px-5 pb-16 sm:px-8 lg:pb-24" aria-labelledby="channels">
          <div className="grid gap-4 rounded-[36px] bg-beige/45 p-7 sm:p-10 lg:grid-cols-[1.2fr_1fr] lg:items-center">
            <div>
              <h2 id="channels" className="text-[28px] font-semibold leading-[1.1] tracking-[-0.03em] sm:text-[34px]">
                Works the way you walk the farm.
              </h2>
              <p className="mt-3 max-w-lg text-[15px] leading-relaxed text-ink-3">
                Install FarmHand on your phone&apos;s home screen. It opens like an app, keeps unsent reports when you lose signal and works with one hand.
              </p>
            </div>
            <ul className="grid grid-cols-2 gap-3 text-sm font-semibold sm:grid-cols-4 lg:grid-cols-2">
              {[
                { icon: AudioLines, label: "Voice notes" },
                { icon: Camera, label: "Photos" },
                { icon: Type, label: "Quick text" },
                { icon: Smartphone, label: "Installable app" },
              ].map((c) => (
                <li key={c.label} className="flex items-center gap-2.5 rounded-2xl bg-paper px-4 py-3">
                  <c.icon className="size-5 text-terracotta-ink" aria-hidden />
                  {c.label}
                </li>
              ))}
            </ul>
          </div>
        </section>
      </main>

      <footer className="border-t border-line">
        <div className="mx-auto flex max-w-[1200px] flex-col gap-3 px-5 py-8 text-sm text-muted sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <p>FarmHand helps you keep records and reach a vet. It does not replace veterinary care.</p>
          <Link href="/about" className="font-semibold text-ink hover:underline">
            About, privacy and photo credits
          </Link>
        </div>
      </footer>
    </div>
  );
}
