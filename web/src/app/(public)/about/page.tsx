import type { Metadata } from "next";
import Link from "next/link";
import credits from "../../../../public/images/credits.json";
import { Brand } from "@/components/shell/brand";

export const metadata: Metadata = { title: "About FarmHand" };

type Credit = { file: string; title: string; creator: string; license: string; source: string; url: string };

export default function AboutPage() {
  return (
    <div className="min-h-dvh bg-cream">
      <header className="mx-auto max-w-3xl px-5 pt-[max(16px,env(safe-area-inset-top))] sm:px-8">
        <Brand />
      </header>
      <main className="mx-auto max-w-3xl space-y-10 px-5 py-10 sm:px-8">
        <section>
          <h1 className="text-[34px] font-semibold tracking-[-0.03em]">About FarmHand</h1>
          <p className="mt-3 text-[15px] leading-relaxed text-muted">
            FarmHand helps livestock and poultry farmers record health concerns, triage them consistently, manage cases and coordinate with veterinarians. It was built for the Team HackForge hackathon entry.
          </p>
        </section>
        <section className="space-y-3 text-[15px] leading-relaxed text-muted">
          <h2 className="text-xl font-semibold text-ink">What FarmHand is not</h2>
          <p>
            FarmHand does not diagnose disease. Image, voice and text analysis produce triage suggestions — symptoms it noticed and how urgent a report looks — which the farmer reviews and confirms. When no AI provider is configured or it is unavailable, a rule-based check is used and labelled as such. A qualified veterinarian makes clinical decisions.
          </p>
        </section>
        <section className="space-y-3 text-[15px] leading-relaxed text-muted">
          <h2 className="text-xl font-semibold text-ink">Privacy</h2>
          <p>
            Sign-in tokens are kept in secure, HTTP-only cookies and never in browser storage. Unsent report drafts are stored on your device only and are erased when you sign out. Veterinarians see a farmer&apos;s contact details only after accepting their request.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold">Photography</h2>
          <p className="mt-2 text-[15px] text-muted">Farm photography is public-domain or Creative Commons-licensed. Each credit below links to the original.</p>
          <ul className="mt-4 divide-y divide-line rounded-[24px] bg-paper ring-1 ring-line/60">
            {(credits as Credit[]).map((c) => (
              <li key={c.file} className="px-5 py-3 text-sm">
                <a href={c.url} className="font-semibold text-ink hover:underline" rel="noopener noreferrer" target="_blank">
                  {c.title}
                </a>
                <span className="text-muted"> — {c.creator} · {c.license} via {c.source}</span>
              </li>
            ))}
          </ul>
        </section>
        <Link href="/" className="inline-block text-sm font-semibold text-muted hover:text-ink">
          ← Back to FarmHand
        </Link>
      </main>
    </div>
  );
}
