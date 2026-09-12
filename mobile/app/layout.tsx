import type { Metadata, Viewport } from "next";
import { Geist } from "next/font/google";
import "./globals.css";
import PWAInstaller from "@/components/PWAInstaller";

const geist = Geist({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Mi Tarjeta Fidelidad",
  description: "Puntos, sellos y cupones digitales de tu marca favorita.",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: "/icons/icon-192.png",
    apple: "/icons/apple-touch-icon.png",
  },
  appleWebApp: { capable: true, statusBarStyle: "black-translucent", title: "Fidelidad" },
};

export const viewport: Viewport = {
  themeColor: "#0b0f19",
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false, // app touch, evita zoom accidental en inputs
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="es" className={geist.className}>
      <body>
        {children}
        <PWAInstaller />
      </body>
    </html>
  );
}