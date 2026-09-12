"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "@/lib/session";
import QRCode from "./QRCode";

export default function Dashboard() {
  const { profile, loading, token } = useSession();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !token) router.replace("/app/register");
  }, [loading, token, router]);

  if (loading) {
    return <div className="center muted">Cargando tu tarjeta…</div>;
  }
  if (!profile) return null;

  return (
    <div>
      <header className="row" style={{ marginBottom: 16 }}>
        <div>
          <div className="muted">Hola,</div>
          <h1 style={{ margin: 0 }}>{profile.fullName}</h1>
        </div>
        <span className="muted">{profile.tenantSlug}</span>
      </header>

      {/* Saldo + puntos */}
      <section className="hero">
        <div className="row">
          <span className="muted">Tus puntos</span>
          <span className="hide-nonstandalone muted" style={{ fontSize: 12 }}>online</span>
        </div>
        <div className="balance">{profile.pointsBalance.toLocaleString("es-CL")}</div>
        <div className="muted" style={{ fontSize: 13 }}>puntos disponibles</div>
      </section>

      {/* QR de fidelidad */}
      <section className="card" style={{ marginTop: 14 }}>
        <div className="muted" style={{ marginBottom: 10 }}>Tu código para canjear en caja</div>
        <QRCode value={profile.qrHash} />
        <div className="qr-hash">{profile.qrHash}</div>
      </section>

      {/* Sellos por producto */}
      <section className="card">
        <div className="row">
          <span>Mis sellos</span>
          <span className="muted" style={{ fontSize: 12 }}>
            {profile.stamps.length} producto{profile.stamps.length === 1 ? "" : "s"}
          </span>
        </div>
        {profile.stamps.length === 0 ? (
          <p className="muted" style={{ fontSize: 14 }}>Aún no tienes sellos acumulados.</p>
        ) : (
          <div className="stamps">
            {profile.stamps.map((s) => (
              <div className="stamp-chip" key={s.productSku}>
                <span>{s.productName}</span>
                <div className="circles">
                  {Array.from({ length: s.stampsRequired }).map((_, i) => (
                    <span key={i} className={`stamp-dot ${i < s.count ? "filled" : ""}`} />
                  ))}
                </div>
                <span className="muted">
                  {s.count}/{s.stampsRequired}
                  {s.completed ? " ✓" : ""}
                </span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}