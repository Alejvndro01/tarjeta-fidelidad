"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { SessionProvider } from "@/lib/session";

const NAV = [
  { href: "/app", label: "Inicio", icon: "🏠" },
  { href: "/app/cupones", label: "Cupones", icon: "🎟️" },
  { href: "/app/ajustes", label: "Ajustes", icon: "⚙️" },
];

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  return (
    <SessionProvider>
      <div className="app-root">
        <main className="app-main">{children}</main>
        <nav className="bottom-nav">
          {NAV.map((n) => {
            const active = pathname === n.href || (n.href !== "/app" && pathname.startsWith(n.href));
            return (
              <Link key={n.href} href={n.href} className={`nav-item ${active ? "active" : ""}`}>
                <span>{n.icon}</span>
                <span>{n.label}</span>
              </Link>
            );
          })}
        </nav>
      </div>
    </SessionProvider>
  );
}