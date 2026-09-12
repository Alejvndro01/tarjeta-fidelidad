"use client";

import { useEffect, useState } from "react";
import { useSession } from "@/lib/session";
import { api, downloadApplePass, fetchGooglePass } from "@/lib/api";
import QRCode from "qrcode";

const isSafariIOS = () =>
  typeof navigator !== "undefined" && /iP(hone|ad|od)/.test(navigator.userAgent) && /Safari/.test(navigator.userAgent);

interface CouponItem {
  couponId: string;
  couponCode: string;
  title: string;
  description: string;
  issuedUtc: string;
  expiresUtc: string | null;
  status: string;
}

export default function CuponesPage() {
  const { profile, token } = useSession();
  const [googleUrl, setGoogleUrl] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const [coupons, setCoupons] = useState<CouponItem[]>([]);
  const [couponQRs, setCouponQRs] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!token) return;
    api.get<CouponItem[]>("/api/members/coupons", { headers: { Authorization: `Bearer ${token}` } })
      .then(async (r) => {
        setCoupons(r.data);
        // Generar QR para cada cupón (client-side con qrcode)
        const qrMap: Record<string, string> = {};
        for (const c of r.data) {
          qrMap[c.couponCode] = await QRCode.toDataURL(c.couponCode, { width: 180, margin: 2, color: { dark: "#1a1a2e" } });
        }
        setCouponQRs(qrMap);
      })
      .catch(() => { /* sin sesión o error — se queda vacío */ });
  }, [token]);

  async function addGoogle() {
    setBusy(true); setMsg(null);
    try {
      const url = await fetchGooglePass();
      if (url) {
        setGoogleUrl(url);
        setMsg("Tu tarjeta está lista para Google Wallet. Tócala para añadirla.");
      } else {
        setMsg("No se pudo generar el pase de Google.");
      }
    } catch {
      setMsg("Error al conectar con la API.");
    } finally {
      setBusy(false);
    }
  }

  async function addApple() {
    setBusy(true); setMsg(null);
    const ok = await downloadApplePass();
    setMsg(ok
      ? (isSafariIOS() ? "Descargado. Ábrelo en Archivos o Safari para instalarlo en Apple Wallet." : "Descargado tu pase (.pkpass). Ábrelo para añadirlo a tu cartera.")
      : "No se pudo generar el pase de Apple.");
    setBusy(false);
  }

  return (
    <div>
      <h1 style={{ marginTop: 4 }}>Mi tarjeta</h1>

      <section className="card">
        <div className="center" style={{ padding: "4px 0 12px" }}>
          <div style={{ fontSize: 40 }}>💳</div>
          <h2 style={{ margin: "8px 0 2px", fontSize: 17 }}>¿Tu tarjeta en el bolsillo?</h2>
          <p className="muted" style={{ fontSize: 14, margin: "0 0 16px" }}>
            Guárdala en tu wallet de Apple o Google para tenerla siempre a mano, sin abrir la app.
          </p>
        </div>

        <button className="btn" onClick={addApple} disabled={busy} style={{ marginBottom: 10 }}>
          🍎 {busy ? "Generando…" : "Guardar en Apple Wallet"}
        </button>
        <button className="btn secondary" onClick={addGoogle} disabled={busy} style={{ marginBottom: 10 }}>
          ⬤ {busy ? "Generando…" : "Añadir a Google Wallet"}
        </button>

        {googleUrl && (
          <div className="center" style={{ marginTop: 10 }}>
            <a
              href={googleUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{ display: "inline-flex", alignItems: "center", gap: 6, color: "var(--accent)", fontWeight: 600 }}
            >
               <span style={{ fontSize: 20 }}>🟢</span> Añadir a Google Wallet
            </a>
          </div>
        )}

        {msg && <p className="muted center" style={{ fontSize: 13, marginTop: 12 }}>{msg}</p>}
      </section>

      {/* Cupones activos del miembro — escanear el QR en la caja para canjear */}
      <section className="card">
        <h2 style={{ margin: "0 0 10px", fontSize: 16 }}>🎟️ Mis cupones</h2>
        {coupons.length === 0 ? (
          <p className="muted center" style={{ fontSize: 14, margin: "8px 0" }}>
            Aún no tienes cupones. Alcanza los sellos de un producto para ganar uno.
          </p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            {coupons.map((c) => (
              <div key={c.couponId} style={{ padding: "12px 0", borderBottom: "1px solid var(--border)" }}>
                <div style={{ fontWeight: 700, fontSize: 15 }}>{c.title}</div>
                <div className="muted" style={{ fontSize: 12, margin: "2px 0 8px" }}>{c.description}</div>
                {couponQRs[c.couponCode] && (
                  <div className="center">
                    <img
                      src={couponQRs[c.couponCode]}
                      alt={`QR ${c.couponCode}`}
                      style={{ width: 140, height: 140, borderRadius: 10, border: "2px solid var(--accent)" }}
                    />
                  </div>
                )}
                <div className="center muted" style={{ fontSize: 11, fontFamily: "monospace", marginTop: 6, letterSpacing: 1.5 }}>
                  {c.couponCode}
                </div>
                <div className="center muted" style={{ fontSize: 11, marginTop: 4 }}>
                  {c.expiresUtc ? `Válido hasta ${new Date(c.expiresUtc).toLocaleDateString("es-CL")}` : "Sin expiración"}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}