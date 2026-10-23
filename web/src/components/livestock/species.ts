/** Species presets offered in forms. Stored as free text (≤ 50 chars) so farmers can be specific. */
export const SPECIES = ["Chicken", "Goat", "Sheep", "Cattle", "Pig", "Turkey", "Duck", "Rabbit", "Other"] as const;

export const SEX_OPTIONS = [
  { value: "Female", label: "Female" },
  { value: "Male", label: "Male" },
  { value: "Unknown", label: "Not sure" },
];
