"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import QrScanner from "@/components/QrScanner";
import {
  fetchRedeemed,
  fetchStores,
  getStaffToken,
  redeemCoupon,
  RedeemedRecord,
  RedeemResult,
  setStaffToken,
  StoreOption,
} from "@/lib/api";

export default function StaffPanel() {
  const router = useRouter();
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [storeId, setStoreId] = useState("");
  const [code, setCode] = useState("");
  const [result, setResult] = useState<RedeemResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [redeemed, setRedeemed] = useState<RedeemedRecord[]>([]);
  const [staffEmail, setStaffEmail] = useState("");

  // Guard: sin token staff → login
  useEffect(() => {
    if (!getStaffToken()) {
      router.replace("/staff");
      return;
    }
    // Cargar tiendas + historial
    (async () => {
      try {
        const [storeList, history] = await Promise.all([fetchStores(), fetchRedeemed()]);
        setStores(storeList);
        setRedeemed(history);
        if (storeList.length > 0) setStoreId(storeList[0].storeId);
      } catch {
        // token inválido → salir
        setStaffToken(null);
        router.replace("/staff");
      }
    })();
  }, [router]);

  async function doRedeem(e: React.FormEvent) {
    e.preventDefault();
    await redeem(code);
  }

  async function redeem(rawCode: string) {
    const c = rawCode.trim().toUpperCase();
    if (!c || !storeId) return;
    setError(null);
    setResult(null);
    setLoading(true);
    try {
      const res = await redeemCoupon({ couponCode: c, storeId });
      setResult(res);
      setCode("");
      // refrescar historial
      const history = await fetchRedeemed();
      setRedeemed(history);
    } catch (err: unknown) {
      setResult(null);
      const axios = await import("axios");
      if (axios.isAxiosError(err)) {
        const d = err.response?.data as { error?: string; message?: string } | undefined;
        setError(d?.message ?? d?.error ?? "No se pudo canjear.");
      } else {
        setError("Error de conexión.");
      }
    } finally {
      setLoading(false);
    }
  }

  function logout() {
    setStaffToken(null);
    router.replace("/staff");
  }

  return (
    <div className="app-root">
      <main className="app-main">
        <header className="row" style={{ marginBottom: 14 }}>
          <div>
            <h1 style={{ margin: 0, fontSize: 20 }}>Canjear cupón</h1>
            <span className="muted">{staffEmail || "Cajero"}</span>
          </div>
          <button className="btn ghost" onClick={logout} style={{ width: "auto", minHeight: 40 }}>Salir</button>
        </header>

        {/* Selector tienda */}
        <div className="field">
          <label htmlFor="store">Tienda</label>
          <select
            id="store"
            value={storeId}
            onChange={(e) => setStoreId(e.target.value)}
            style={{ width: "100%", minHeight: 52, background: "var(--surface)", color: "var(--text)", border: "1px solid var(--border)", borderRadius: 12, padding: "0 14px" }}
          >
            {stores.length === 0 && <option value="">(sin tiendas)</option>}
            {stores.map((s) => <option key={s.storeId} value={s.storeId}>{s.name}</option>)}
          </select>
        </div>

        {/* Escáner QR del cupón */}
        <section className="card" style={{ marginBottom: 14, paddingTop: 14 }}>
          <QrScanner onScan={(c) => redeem(c)} />
          <div style={{ textAlign: "center", marginTop: 10 }}>
            <span className="muted" style={{ fontSize: 12 }}>
              Escanea el QR del cupón o escribe el código abajo
            </span>
          </div>
        </section>

        {/* Ingreso de código */}
        <form onSubmit={doRedeem}>
          <div className="field">
            <label htmlFor="code">Código del cupón</label>
            <input
              id="code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              placeholder="CPN-XXXX-000"
              style={{ textTransform: "uppercase", letterSpacing: 1.5, fontSize: 18, fontWeight: 600 }}
              disabled={loading}
              autoFocus
            />
          </div>
          <button className="btn" type="submit" disabled={loading || !code.trim() || !storeId}>
            {loading ? "Canjeando…" : "Canjear cupón"}
          </button>
        </form>

        {/* Resultado / error */}
        {result && (
          <div className={`card mt ${result.redeemed ? "" : ""}`}
            style={{ borderColor: result.redeemed ? "var(--ok)" : "var(--danger)" }}>
            <div className="row">
              <div>
                <div style={{ fontWeight: 700, color: result.redeemed ? "var(--ok)" : "var(--danger)" }}>
                  {result.redeemed ? "✓ Cupón canjeado" : "Cupón no válido"}
                </div>
                <div style={{ marginTop: 4 }}>{result.title}</div>
              </div>
              <div className="muted" style={{ fontSize: 13 }}>{result.couponCode}</div>
            </div>
          </div>
        )}
        {error && (
          <div className="card mt" style={{ borderColor: "var(--danger)" }}>
            <div style={{ color: "var(--danger)", fontWeight: 600 }}>✕ {error}</div>
          </div>
        )}

        {/* Historial */}
        <section className="card mt">
          <div className="row" style={{ marginBottom: 10 }}>
            <span style={{ fontWeight: 600 }}>Canjes recientes</span>
            <span className="muted" style={{ fontSize: 12 }}>{redeemed.length}</span>
          </div>
          {redeemed.length === 0 ? (
            <p className="muted" style={{ fontSize: 14 }}>Aún no se han canjeado cupones.</p>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {redeemed.map((r) => (
                <div key={r.couponId} className="row" style={{ padding: "8px 0", borderBottom: "1px solid var(--border)" }}>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: 14 }}>{r.title}</div>
                    <div className="muted" style={{ fontSize: 12 }}>
                      {r.storeName || "—"} · {new Date(r.redeemedUtc).toLocaleTimeString("es-CL")} · {r.redeemedByEmail || ""}
                    </div>
                  </div>
                  <span className="muted" style={{ fontSize: 12, fontFamily: "monospace" }}>{r.couponCode}</span>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}