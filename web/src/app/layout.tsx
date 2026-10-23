import "@fontsource-variable/plus-jakarta-sans";
import "./globals.css";
import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { Providers } from "@/components/providers/providers";
import { ServiceWorkerRegistrar } from "@/components/pwa/service-worker-registrar";

export const metadata: Metadata = {
  title: { default: "FarmHand — livestock health, handled", template: "%s · FarmHand" },
  description:
    "Record a health concern by voice, photo or text, get a clear triage, keep a case file and bring in a vet — built for livestock and poultry farmers.",
  applicationName: "FarmHand",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "FarmHand" },
  formatDetection: { telephone: false },
  icons: {
    icon: [
      { url: "/icons/favicon-32.png", sizes: "32x32", type: "image/png" },
      { url: "/icons/favicon-16.png", sizes: "16x16", type: "image/png" },
    ],
    apple: [{ url: "/icons/apple-touch-icon.png", sizes: "180x180" }],
  },
};

export const viewport: Viewport = {
  themeColor: "#EEEAE1",
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en-NG">
      <body className="antialiased">
        <Providers>{children}</Providers>
        <ServiceWorkerRegistrar />
      </body>
    </html>
  );
}
