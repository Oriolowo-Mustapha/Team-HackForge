"use client";

import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Check, X } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AnimatePresence, motion } from "motion/react";
import { useState, type FormEvent } from "react";
import { farmsApi, livestockApi } from "@/lib/api/endpoints";
import { firstName } from "@/lib/format";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { useSession } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { BrandMark } from "@/components/shell/brand";
import { Button } from "@/components/ui/button";
import { ChoiceChips, Field, Input } from "@/components/ui/field";
import { InlineError } from "@/components/ui/states";

export const SPECIES_OPTIONS = [
  { value: "Poultry (layers)", label: "Layers", group: "Layer flock" },
  { value: "Poultry (broilers)", label: "Broilers", group: "Broiler flock" },
  { value: "Goat", label: "Goats", group: "Goat herd" },
  { value: "Sheep", label: "Sheep", group: "Sheep flock" },
  { value: "Cattle", label: "Cattle", group: "Cattle herd" },
  { value: "Pig", label: "Pigs", group: "Pig pen" },
] as const;

export function SetupFlow({ another }: { another: boolean }) {
  const router = useRouter();
  const client = useQueryClient();
  const { user } = useSession();
  const { setFarm } = useActiveFarm();
  const [step, setStep] = useState<1 | 2>(1);
  const [farmId, setFarmId] = useState<string | null>(null);
  const [farm, setFarmForm] = useState({ name: "", location: "" });
  const [group, setGroup] = useState({ species: "" as string, name: "", headcount: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  async function createFarm(event: FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (farm.name.trim().length < 2) found.name = "Give your farm a name.";
    if (farm.location.trim().length < 2) found.location = "Add a town, LGA or state so vets know where you are.";
    setErrors(found);
    if (Object.keys(found).length) return;
    setPending(true);
    setError(null);
    try {
      const { id } = await farmsApi.create(farm.name.trim(), farm.location.trim());
      setFarmId(id);
      setFarm(id);
      setStep(2);
    } catch (err) {
      setError(err);
    } finally {
      setPending(false);
    }
  }

  async function finish(skip: boolean) {
    if (!farmId) return;
    if (!skip) {
      const found: Record<string, string> = {};
      if (!group.species) found.species = "Choose what you keep.";
      if (group.name.trim().length < 2) found.name = "Name this group, for example “Layer house A”.";
      const count = Number(group.headcount);
      if (group.headcount && (!Number.isInteger(count) || count < 0 || count > 1_000_000)) found.headcount = "Enter a whole number.";
      setErrors(found);
      if (Object.keys(found).length) return;
      setPending(true);
      setError(null);
      try {
        await livestockApi.createGroup(farmId, {
          name: group.name.trim(),
          species: group.species,
          headcount: group.headcount ? count : null,
          description: null,
        });
      } catch (err) {
        setError(err);
        setPending(false);
        return;
      }
    }
    setPending(true);
    await client.invalidateQueries({ queryKey: qk.session });
    await client.invalidateQueries({ queryKey: qk.farms });
    router.replace("/home");
  }

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
      <div className="relative hidden overflow-hidden lg:sticky lg:top-3 lg:m-3 lg:block lg:h-[calc(100dvh-24px)] lg:rounded-[36px]">
        <Image src="/images/goats-pen.webp" alt="" fill priority sizes="45vw" className="object-cover" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink/70 to-transparent" aria-hidden />
        <p className="absolute inset-x-10 bottom-10 text-[32px] font-semibold leading-[1.1] tracking-[-0.03em] text-cream text-balance">
          Every report links to a farm and the animals on it. Let&apos;s add yours.
        </p>
      </div>

      <main className="mx-auto w-full max-w-[520px] px-5 pb-12 pt-[max(20px,env(safe-area-inset-top))] sm:px-8 lg:flex lg:flex-col lg:justify-center lg:py-16">
        <div className="mb-8 flex items-center justify-between">
          <BrandMark className="size-10" />
          <div className="flex items-center gap-2" aria-label={`Step ${step} of 2`}>
            {[1, 2].map((s) => (
              <span key={s} className={`h-1.5 rounded-full transition-all ${s <= step ? "w-8 bg-ink" : "w-4 bg-line"}`} aria-hidden />
            ))}
          </div>
          {another ? (
            <Link href="/home" aria-label="Cancel" className="inline-flex size-11 items-center justify-center rounded-2xl bg-paper shadow-soft">
              <X className="size-5" />
            </Link>
          ) : (
            <span className="size-11" aria-hidden />
          )}
        </div>

        <AnimatePresence mode="wait" initial={false}>
          {step === 1 ? (
            <motion.form key="farm" onSubmit={createFarm} initial={{ opacity: 0, x: 24 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -24 }} className="space-y-5" noValidate>
              <div>
                <p className="text-sm font-semibold text-muted">{another ? "New farm" : `Welcome, ${firstName(user?.fullName)}`}</p>
                <h1 className="mt-1 text-[30px] font-semibold leading-tight tracking-[-0.03em]">Tell us about your farm</h1>
                <p className="mt-2 text-[15px] leading-relaxed text-muted">Vets see the location when you ask for help, so they know how far they&apos;d travel.</p>
              </div>
              <Field label="Farm name" error={errors.name}>
                <Input value={farm.name} onChange={(e) => setFarmForm((f) => ({ ...f, name: e.target.value }))} placeholder="Adeyemi Family Farm" autoComplete="organization" />
              </Field>
              <Field label="Location" hint="Town and state, e.g. Ikorodu, Lagos" error={errors.location}>
                <Input value={farm.location} onChange={(e) => setFarmForm((f) => ({ ...f, location: e.target.value }))} placeholder="Ikorodu, Lagos" autoComplete="address-level2" />
              </Field>
              <InlineError error={error} />
              <Button type="submit" size="lg" block loading={pending}>
                Continue
              </Button>
            </motion.form>
          ) : (
            <motion.div key="group" initial={{ opacity: 0, x: 24 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -24 }} className="space-y-5">
              <div>
                <p className="inline-flex items-center gap-1.5 text-sm font-semibold text-green-ink">
                  <Check className="size-4" aria-hidden /> {farm.name} created
                </p>
                <h1 className="mt-1 text-[30px] font-semibold leading-tight tracking-[-0.03em]">What do you keep?</h1>
                <p className="mt-2 text-[15px] leading-relaxed text-muted">
                  Add one flock, herd or pen. You can register individual animals later — useful for cattle and goats you track by name or tag.
                </p>
              </div>
              <div className="space-y-2">
                <p className="text-sm font-semibold" id="species-label">
                  Species
                </p>
                <ChoiceChips
                  label="Species"
                  options={SPECIES_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                  value={group.species || null}
                  onChange={(value) => {
                    const preset = SPECIES_OPTIONS.find((o) => o.value === value);
                    setGroup((g) => ({ ...g, species: value, name: g.name || preset?.group || "" }));
                  }}
                />
                {errors.species && (
                  <p role="alert" className="text-[13px] font-medium text-red-ink">
                    {errors.species}
                  </p>
                )}
              </div>
              <Field label="Group name" error={errors.name}>
                <Input value={group.name} onChange={(e) => setGroup((g) => ({ ...g, name: e.target.value }))} placeholder="Layer house A" />
              </Field>
              <Field label="How many animals?" optional error={errors.headcount}>
                <Input inputMode="numeric" value={group.headcount} onChange={(e) => setGroup((g) => ({ ...g, headcount: e.target.value.replace(/[^\d]/g, "") }))} placeholder="250" />
              </Field>
              <InlineError error={error} />
              <Button size="lg" block loading={pending} onClick={() => void finish(false)}>
                Finish setup
              </Button>
              <Button variant="ghost" block disabled={pending} onClick={() => void finish(true)} icon={<ArrowLeft className="size-4 rotate-180" />}>
                Skip for now
              </Button>
            </motion.div>
          )}
        </AnimatePresence>
      </main>
    </div>
  );
}
