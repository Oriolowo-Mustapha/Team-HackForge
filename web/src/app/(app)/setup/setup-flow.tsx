"use client";

import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Check, Loader2, MapPin, Navigation, RotateCcw, X } from "lucide-react";
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
import { Segmented } from "@/components/ui/segmented";
import { InlineError } from "@/components/ui/states";

export const SPECIES_OPTIONS = [
  { value: "Poultry (layers)", label: "Layers", group: "Layer flock" },
  { value: "Poultry (broilers)", label: "Broilers", group: "Broiler flock" },
  { value: "Goat", label: "Goats", group: "Goat herd" },
  { value: "Sheep", label: "Sheep", group: "Sheep flock" },
  { value: "Cattle", label: "Cattle", group: "Cattle herd" },
  { value: "Pig", label: "Pigs", group: "Pig pen" },
] as const;

export const NIGERIAN_STATES = [
  "Abia", "Adamawa", "Akwa Ibom", "Anambra", "Bauchi", "Bayelsa", "Benue", "Borno",
  "Cross River", "Delta", "Ebonyi", "Edo", "Ekiti", "Enugu", "FCT - Abuja", "Gombe",
  "Imo", "Jigawa", "Kaduna", "Kano", "Katsina", "Kebbi", "Kogi", "Kwara", "Lagos",
  "Nasarawa", "Niger", "Ogun", "Ondo", "Osun", "Oyo", "Plateau", "Rivers", "Sokoto",
  "Taraba", "Yobe", "Zamfara"
] as const;

export function SetupFlow({ another }: { another: boolean }) {
  const router = useRouter();
  const client = useQueryClient();
  const { user } = useSession();
  const { setFarm } = useActiveFarm();
  const [step, setStep] = useState<1 | 2>(1);
  const [farmId, setFarmId] = useState<string | null>(null);
  const [locationMode, setLocationMode] = useState<"gps" | "manual">("gps");
  const [locating, setLocating] = useState(false);
  const [geoError, setGeoError] = useState<string | null>(null);
  const [farm, setFarmForm] = useState<{
    name: string;
    location: string;
    cityOrTown: string;
    state: string;
    country: string;
    latitude: number | null;
    longitude: number | null;
    locationSource: string;
  }>({
    name: "",
    location: "",
    cityOrTown: "",
    state: "",
    country: "Nigeria",
    latitude: null,
    longitude: null,
    locationSource: "device_gps",
  });
  const [group, setGroup] = useState({ species: "" as string, name: "", headcount: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  function detectGpsLocation() {
    if (typeof window === "undefined" || !("geolocation" in navigator)) {
      setGeoError("Geolocation is not supported by your browser. Please enter your location manually.");
      setLocationMode("manual");
      return;
    }
    setLocating(true);
    setGeoError(null);
    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        const lat = pos.coords.latitude;
        const lon = pos.coords.longitude;
        try {
          const res = await fetch(
            `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lon}&zoom=14&addressdetails=1`,
            { headers: { "Accept-Language": "en" } }
          );
          if (!res.ok) throw new Error("Reverse geocode request failed");
          const data = await res.json();
          const addr = data.address || {};
          const cityOrTown = addr.town || addr.city || addr.suburb || addr.village || addr.county || "";
          const state = addr.state || "";
          const country = addr.country || "Nigeria";
          const formatted = [cityOrTown, state, country].filter(Boolean).join(", ") || data.display_name || `${lat.toFixed(4)}, ${lon.toFixed(4)}`;

          setFarmForm((prev) => ({
            ...prev,
            location: formatted,
            cityOrTown,
            state,
            country,
            latitude: lat,
            longitude: lon,
            locationSource: "device_gps",
          }));
        } catch {
          const fallback = `${lat.toFixed(4)}, ${lon.toFixed(4)}`;
          setFarmForm((prev) => ({
            ...prev,
            location: fallback,
            latitude: lat,
            longitude: lon,
            locationSource: "device_gps",
          }));
        } finally {
          setLocating(false);
        }
      },
      (err) => {
        setLocating(false);
        if (err.code === 1) {
          setGeoError("Location access was denied. You can enter your town and state manually below.");
        } else {
          setGeoError("GPS coordinates could not be retrieved. Please enter your location manually.");
        }
        setLocationMode("manual");
      },
      { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
    );
  }

  function handleManualChange(field: "cityOrTown" | "state" | "location", val: string) {
    setFarmForm((prev) => {
      const next = { ...prev, [field]: val, locationSource: "manual" };
      if (field === "cityOrTown" || field === "state") {
        const parts = [next.cityOrTown.trim(), next.state.trim(), next.country].filter(Boolean);
        next.location = parts.join(", ");
      }
      return next;
    });
  }

  async function createFarm(event: FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (farm.name.trim().length < 2) found.name = "Give your farm a name.";
    if (farm.location.trim().length < 2) found.location = "Add a location or use GPS so vets know where you are.";
    setErrors(found);
    if (Object.keys(found).length) return;
    setPending(true);
    setError(null);
    try {
      const { id } = await farmsApi.create({
        displayName: farm.name.trim(),
        location: farm.location.trim(),
        latitude: farm.latitude,
        longitude: farm.longitude,
        cityOrTown: farm.cityOrTown || null,
        state: farm.state || null,
        country: farm.country || "Nigeria",
        locationSource: farm.locationSource || (farm.latitude ? "device_gps" : "manual"),
      });
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

              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-semibold">Farm location</p>
                  <span className="text-xs text-muted">Choose method</span>
                </div>
                <Segmented
                  label="Location mode"
                  value={locationMode}
                  onChange={(m) => setLocationMode(m as "gps" | "manual")}
                  options={[
                    { value: "gps", label: "Auto-detect (GPS)" },
                    { value: "manual", label: "Type manually" },
                  ]}
                />

                {locationMode === "gps" ? (
                  <div className="space-y-2">
                    {farm.latitude && farm.longitude ? (
                      <div className="rounded-2xl border border-green-ink/20 bg-green-ink/5 p-4 space-y-2">
                        <div className="flex items-start justify-between gap-3">
                          <div className="flex items-center gap-2 text-green-ink font-semibold text-sm">
                            <MapPin className="size-4 shrink-0" />
                            <span>GPS Location Locked</span>
                          </div>
                          <span className="inline-flex items-center rounded-full bg-green-ink/10 px-2.5 py-0.5 text-xs font-medium text-green-ink tabular">
                            {farm.latitude.toFixed(4)}, {farm.longitude.toFixed(4)}
                          </span>
                        </div>
                        <p className="text-sm font-medium text-ink leading-snug">{farm.location}</p>
                        <div className="pt-1">
                          <button
                            type="button"
                            onClick={detectGpsLocation}
                            disabled={locating}
                            className="inline-flex items-center gap-1.5 text-xs font-semibold text-muted hover:text-ink transition-colors"
                          >
                            <RotateCcw className="size-3" /> Re-detect location
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="rounded-2xl border border-line bg-paper p-5 text-center space-y-3">
                        <div className="mx-auto flex size-11 items-center justify-center rounded-full bg-sand text-ink">
                          {locating ? <Loader2 className="size-5 animate-spin" /> : <Navigation className="size-5" />}
                        </div>
                        <div>
                          <p className="text-sm font-semibold">Get current device location</p>
                          <p className="text-xs text-muted mt-0.5">Captures your precise coordinates and resolves your town and state automatically.</p>
                        </div>
                        <Button
                          type="button"
                          variant="secondary"
                          size="md"
                          onClick={detectGpsLocation}
                          loading={locating}
                          icon={<MapPin className="size-4" />}
                        >
                          Detect My Location
                        </Button>
                      </div>
                    )}
                    {geoError && (
                      <p role="alert" className="text-xs text-red-ink font-medium">
                        {geoError}
                      </p>
                    )}
                    {errors.location && (
                      <p role="alert" className="text-xs text-red-ink font-medium">
                        {errors.location}
                      </p>
                    )}
                  </div>
                ) : (
                  <div className="space-y-3">
                    <Field label="Town or City / LGA" hint="e.g. Ikorodu, Epe, Ibadan, Zaria">
                      <Input
                        value={farm.cityOrTown}
                        onChange={(e) => handleManualChange("cityOrTown", e.target.value)}
                        placeholder="Ikorodu"
                        autoComplete="address-level2"
                      />
                    </Field>
                    <Field label="State" hint="Select your state">
                      <div className="relative">
                        <select
                          value={farm.state}
                          onChange={(e) => handleManualChange("state", e.target.value)}
                          className="w-full h-12 rounded-2xl border border-line bg-paper px-4 text-sm font-medium text-ink focus:outline-none focus:ring-2 focus:ring-ink"
                        >
                          <option value="">Select state...</option>
                          {NIGERIAN_STATES.map((s) => (
                            <option key={s} value={s}>{s}</option>
                          ))}
                        </select>
                      </div>
                    </Field>
                    <Field label="Full Location Summary" hint="Formatted address stored in records" error={errors.location}>
                      <Input
                        value={farm.location}
                        onChange={(e) => handleManualChange("location", e.target.value)}
                        placeholder="Ikorodu, Lagos, Nigeria"
                        autoComplete="address-level1"
                      />
                    </Field>
                  </div>
                )}
              </div>
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
