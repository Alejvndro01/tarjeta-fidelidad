"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  addReward,
  addStampRule,
  createCoupon,
  exportReportCsv,
  fetchAdminCoupons,
  fetchAdminDashboard,
  fetchAdminMembers,
  fetchAdminProgram,
  fetchAdminReport,
  fetchAdminTransactions,
  getAdminToken,
  setAdminToken,
  updateProgramPoints,
  type AdminCoupon,
  type AdminDashboard,
  type AdminMember,
  type AdminProgram,
  type AdminReport,
  type AdminTransaction,
} from "@/lib/api";

type Tab = "resumen" | "miembros" | "cupones" | "programa" | "transacciones" | "reportes";
const fmt = (n: number) => (typeof Intl !== "undefined" ? new Intl.NumberFormat("es-CL").format(n) : String(n));
const fmtDate = (s?: string | null) => (s ? new Date(s).toLocaleString("es-CL", { dateStyle: "short", timeStyle: "short" }) : "—");

export default function AdminDashboard() {
  const router = useRouter();
  const [tab, setTab] = useState<Tab>("resumen");
  const [dash, setDash] = useState<AdminDashboard | null>(null);
  const [members, setMembers] = useState<AdminMember[]>([]);
  const [coupons, setCoupons] = useState<AdminCoupon[]>([]);
  const [program, setProgram] = useState<AdminProgram | null>(null);
  const [transactions, setTransactions] = useState<AdminTransaction[]>([]);
  const [report, setReport] = useState<AdminReport | null>(null);
  const [reportFrom, setReportFrom] = useState(() => new Date(Date.now() - 30 * 864e5).toISOString().slice(0, 10));
  const [reportTo, setReportTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [search, setSearch] = useState("");
  const [msg, setMsg] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  // Form estado
  const [ppu, setPpu] = useState("1");
  const [ptsOn, setPtsOn] = useState(true);
  const [stampsOn, setStampsOn] = useState(true);
  const [sku, setSku] = useState("");
  const [skuName, setSkuName] = useState("");
  const [reqStamps, setReqStamps] = useState("3");
  const [rewName, setRewName] = useState("");
  const [rewDesc, setRewDesc] = useState("");
  const [rewPts, setRewPts] = useState("0");
  const [rewStamps, setRewStamps] = useState("0");
  const [cpnMember, setCpnMember] = useState("");
  const [cpnTitle, setCpnTitle] = useState("");

  useEffect(() => {
    if (!getAdminToken()) {
      router.replace("/admin");
      return;
    }
    void load("resumen");
  }, []);

  async function load(target: Tab) {
    setTab(target);
    setErr(null);
    try {
      if (target === "resumen") setDash(await fetchAdminDashboard());
      else if (target === "miembros") setMembers((await fetchAdminMembers(1, 200)).items);
      else if (target === "cupones") setCoupons((await fetchAdminCoupons(1, 200)).items);
      else if (target === "programa") {
        const p = await fetchAdminProgram();
        setProgram(p);
        setPpu(String(p.pointsPerMonetaryUnit));
        setPtsOn(p.pointsEnabled);
        setStampsOn(p.stampsEnabled);
      }
      else if (target === "transacciones") setTransactions((await fetchAdminTransactions(1, 200)).items);
      else if (target === "reportes") await loadReport();
    } catch (e: unknown) {
      const axios = await import("axios");
      if (axios.isAxiosError(e) && e.response?.status === 401) {
        setAdminToken(null);
        router.replace("/admin");
        return;
      }
      setErr("No se pudo cargar el panel.");
    }
  }

  async function doSearch() {
    setMembers((await fetchAdminMembers(1, 200, search)).items);
  }

  async function savePoints() {
    setMsg(null); setErr(null);
    try {
      await updateProgramPoints({ pointsEnabled: ptsOn, stampsEnabled: stampsOn, pointsPerMonetaryUnit: Math.max(1, Number(ppu) || 1) });
      setMsg("Configuración de puntos guardada (cache de caja invalidado).");
    } catch { setErr("No se pudo guardar."); }
  }

  async function doAddStamp() {
    setMsg(null); setErr(null);
    try {
      await addStampRule({ productSku: sku.trim(), productName: skuName.trim(), stampsRequired: Math.max(1, Number(reqStamps) || 1) });
      setSku(""); setSkuName(""); setReqStamps("3");
      setMsg("Regla de sellos agregada.");
      setProgram(await fetchAdminProgram());
    } catch { setErr("No se pudo agregar la regla."); }
  }

  async function doAddReward() {
    setMsg(null); setErr(null);
    try {
      await addReward({ name: rewName.trim(), description: rewDesc.trim() || undefined, pointsCost: Number(rewPts) || 0, stampCost: Number(rewStamps) || 0 });
      setRewName(""); setRewDesc(""); setRewPts("0"); setRewStamps("0");
      setMsg("Recompensa agregada.");
      setProgram(await fetchAdminProgram());
    } catch { setErr("No se pudo agregar la recompensa."); }
  }

  async function doCreateCoupon() {
    setMsg(null); setErr(null);
    try {
      const res = await createCoupon({ memberId: cpnMember.trim(), title: cpnTitle.trim() });
      setCpnMember(""); setCpnTitle("");
      setMsg(`Cupón creado: ${res.couponCode}`);
    } catch (e: unknown) {
      const axios = await import("axios");
      if (axios.isAxiosError(e)) {
        const d = e.response?.data as { error?: string; message?: string } | undefined;
        setErr(d?.message ?? d?.error ?? "No se pudo crear el cupón.");
      } else setErr("Error de conexión.");
    }
  }

  function logout() {
    setAdminToken(null);
    router.replace("/admin");
  }

  async function loadReport() {
    setErr(null);
    try {
      // Enviar el rango como ISO con límites de día (from 00:00, to 23:59:59) para incluir el día completo.
      const from = `${reportFrom}T00:00:00.000Z`;
      const to = `${reportTo}T23:59:59.999Z`;
      setReport(await fetchAdminReport(from, to));
    } catch {
      setErr("No se pudo cargar el reporte.");
    }
  }

  async function doExport() {
    setErr(null);
    try {
      await exportReportCsv(`${reportFrom}T00:00:00.000Z`, `${reportTo}T23:59:59.999Z`);
      setMsg("Reporte exportado (CSV).");
    } catch { setErr("No se pudo exportar."); }
  }

  const nav: Tab[] = ["resumen", "miembros", "cupones", "programa", "transacciones", "reportes"];

  return (
    <div className="app-root">
      <main className="app-main">
        <header className="row" style={{ marginBottom: 14 }}>
          <h1 style={{ margin: 0, fontSize: 20 }}>Administración</h1>
          <button className="btn ghost" onClick={logout} style={{ width: "auto", minHeight: 40 }}>Salir</button>
        </header>

        {/* Tabs */}
        <div className="row" style={{ gap: 6, flexWrap: "wrap", marginBottom: 16 }}>
          {nav.map((t) => (
            <button key={t} type="button" onClick={() => load(t)}
              style={{
                padding: "8px 14px", borderRadius: 999, fontSize: 13, fontWeight: 600, cursor: "pointer", border: "1px solid var(--border)",
                background: tab === t ? "var(--accent)" : "var(--surface)", color: tab === t ? "#fff" : "var(--text)",
              }}>
              {t[0].toUpperCase() + t.slice(1)}
            </button>
          ))}
        </div>

        {(msg || err) && (
          <div className="card" style={{ marginBottom: 14, borderColor: err ? "var(--danger)" : "var(--ok)" }}>
            <span style={{ color: err ? "var(--danger)" : "var(--ok)" }}>{err ?? msg}</span>
          </div>
        )}

        {/* RESUMEN */}
        {tab === "resumen" && dash && (
          <div style={{ display: "grid", gap: 12 }}>
            {[
              ["👥 Miembros", fmt(dash.memberCount)],
              ["🎟️ Cupones activos", fmt(dash.activeCoupons)],
              ["✅ Cupones canjeados", fmt(dash.redeemedCoupons)],
              ["⭐ Puntos emitidos", fmt(dash.totalPointsIssued)],
              ["🔻 Puntos canjeados", fmt(dash.totalPointsRedeemed)],
            ].map(([label, val]) => (
              <div key={label} className="card row">
                <span className="muted" style={{ fontSize: 14 }}>{label}</span>
                <span style={{ fontWeight: 700, fontSize: 20 }}>{val}</span>
              </div>
            ))}
          </div>
        )}

        {/* MIEMBROS */}
        {tab === "miembros" && (
          <div>
            <div className="row" style={{ marginBottom: 12 }}>
              <input value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => e.key === "Enter" && doSearch()}
                placeholder="Buscar por nombre, celular o correo" style={{ flex: 1, minHeight: 44 }} />
              <button className="btn secondary" onClick={doSearch} style={{ width: "auto", minHeight: 44 }}>Buscar</button>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {members.length === 0 && <p className="muted">Sin resultados.</p>}
              {members.map((m) => (
                <div key={m.memberId} className="card" style={{ padding: "10px 14px" }}>
                  <div className="row">
                    <div>
                      <div style={{ fontWeight: 600 }}>{m.fullName}</div>
                      <div className="muted" style={{ fontSize: 12 }}>{m.phoneNumber} · {m.qrHash} · {m.email || "sin correo"}</div>
                      <div className="muted" style={{ fontSize: 12 }}>Alta: {fmtDate(m.joinedAtUtc)}</div>
                    </div>
                    <span style={{ fontWeight: 700 }}>{fmt(m.pointsBalance)} pts</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* CUPONES */}
        {tab === "cupones" && (
          <div>
            <section className="card" style={{ marginBottom: 14, paddingTop: 14 }}>
              <h2 style={{ fontSize: 15, margin: "0 0 10px" }}>➕ Crear cupón manual (campaña)</h2>
              <div className="field"><label>MemberId del miembro</label>
                <input value={cpnMember} onChange={(e) => setCpnMember(e.target.value)} placeholder="e.g. d9e86fb5-…" /></div>
              <div className="field"><label>Título</label>
                <input value={cpnTitle} onChange={(e) => setCpnTitle(e.target.value)} placeholder="Ej: Combo 2x1" /></div>
              <button className="btn" onClick={doCreateCoupon} disabled={!cpnMember.trim() || !cpnTitle.trim()}>Crear cupón</button>

              <div className="field" style={{ marginTop: 12 }}>
                <label>Crear desde un miembro listado</label>
                <select onChange={(e) => setCpnMember(e.target.value)} value={members.find((m) => m.memberId === cpnMember)?.memberId ?? ""}>
                  <option value="">— selecciona miembro —</option>
                  {members.map((m) => <option key={m.memberId} value={m.memberId}>{m.fullName} ({m.phoneNumber})</option>)}
                </select>
              </div>
            </section>

            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {coupons.length === 0 && <p className="muted">Sin cupones.</p>}
              {coupons.map((c) => (
                <div key={c.couponId} className="card" style={{ padding: "10px 14px" }}>
                  <div className="row">
                    <div>
                      <div style={{ fontWeight: 600 }}>{c.title}</div>
                      <div className="muted" style={{ fontSize: 12, fontFamily: "monospace" }}>{c.couponCode}</div>
                      <div className="muted" style={{ fontSize: 12 }}>{c.memberName || ""} · {c.memberPhone || ""} · emitido {fmtDate(c.issuedUtc)}</div>
                      {c.redeemedUtc && <div className="muted" style={{ fontSize: 12 }}>Canjeado {fmtDate(c.redeemedUtc)} en {c.storeName || "—"}</div>}
                    </div>
                    <span style={{ fontSize: 12, fontWeight: 700, color: c.status === "Used" ? "var(--ok)" : "var(--accent)" }}>{c.status}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* PROGRAMA */}
        {tab === "programa" && program && (
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <section className="card" style={{ paddingTop: 14 }}>
              <h2 style={{ fontSize: 15, margin: "0 0 10px" }}>⚙️ {program.name}</h2>
              <div className="row">
                <label className="muted">Puntos por unidad monetaria ({program.currencyCode}):
                  <input value={ppu} onChange={(e) => setPpu(e.target.value)} type="number" min="1" style={{ width: 80, marginLeft: 8 }} /></label>
              </div>
              <div className="row" style={{ justifyContent: "flex-start", gap: 20, margin: "10px 0" }}>
                <label className="row" style={{ gap: 6 }}><input type="checkbox" checked={ptsOn} onChange={(e) => setPtsOn(e.target.checked)} /> Puntos</label>
                <label className="row" style={{ gap: 6 }}><input type="checkbox" checked={stampsOn} onChange={(e) => setStampsOn(e.target.checked)} /> Sellos</label>
              </div>
              <button className="btn" onClick={savePoints}>Guardar configuración</button>
            </section>

            <section className="card">
              <h3 style={{ fontSize: 14, margin: "0 0 8px" }}>Reglas de sellos</h3>
              <div className="row" style={{ gap: 6, flexWrap: "wrap" }}>
                <input value={sku} onChange={(e) => setSku(e.target.value.toUpperCase())} placeholder="SKU (Ej: SKU-BURGER)" style={{ minHeight: 42 }} />
                <input value={skuName} onChange={(e) => setSkuName(e.target.value)} placeholder="Producto" style={{ minHeight: 42 }} />
                <input value={reqStamps} onChange={(e) => setReqStamps(e.target.value)} type="number" min="1" placeholder="sellos" style={{ width: 90, minHeight: 42 }} />
                <button className="btn secondary" onClick={doAddStamp} disabled={!sku || !skuName}>+ Regla</button>
              </div>
              {program.stampRules.map((r) => (
                <div key={r.ruleId} className="muted" style={{ fontSize: 13, padding: "8px 0", borderBottom: "1px solid var(--border)" }}>
                  {r.productName} ({r.productSku}) — {r.stampsRequired} sellos
                </div>
              ))}
            </section>

            <section className="card">
              <h3 style={{ fontSize: 14, margin: "0 0 8px" }}>Recompensas canjeables</h3>
              <div className="field"><label>Nombre</label><input value={rewName} onChange={(e) => setRewName(e.target.value)} placeholder="Ej: Combo gratis" /></div>
              <div className="field"><label>Descripción</label><input value={rewDesc} onChange={(e) => setRewDesc(e.target.value)} placeholder="Opcional" /></div>
              <div className="row" style={{ gap: 8 }}>
                <div className="field" style={{ flex: 1 }}><label>Puntos</label><input value={rewPts} onChange={(e) => setRewPts(e.target.value)} type="number" min="0" /></div>
                <div className="field" style={{ flex: 1 }}><label>Sellos</label><input value={rewStamps} onChange={(e) => setRewStamps(e.target.value)} type="number" min="0" /></div>
              </div>
              <button className="btn secondary" onClick={doAddReward} disabled={!rewName}>+ Recompensa</button>
              {program.rewards.map((r) => (
                <div key={r.rewardId} className="muted" style={{ fontSize: 13, padding: "8px 0", borderBottom: "1px solid var(--border)" }}>
                  {r.name} — {r.pointsCost > 0 ? `${r.pointsCost} pts` : ""}{r.stampCost > 0 ? ` · ${r.stampCost} sellos` : ""}
                </div>
              ))}
            </section>
          </div>
        )}

        {/* TRANSACCIONES */}
        {tab === "transacciones" && (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {transactions.length === 0 && <p className="muted">Sin transacciones.</p>}
            {transactions.map((t) => (
              <div key={t.transactionId} className="card" style={{ padding: "10px 14px" }}>
                <div className="row">
                  <div>
                    <div style={{ fontWeight: 600, fontSize: 14 }}>{t.type}</div>
                    <div className="muted" style={{ fontSize: 12 }}>{t.memberName || ""} · {t.reference}</div>
                    <div className="muted" style={{ fontSize: 12 }}>{fmtDate(t.createdUtc)}{t.storeName ? ` · ${t.storeName}` : ""}</div>
                  </div>
                  <span style={{ fontWeight: 700, fontSize: 15 }}>
                    {t.pointsDelta !== 0 ? `${t.pointsDelta > 0 ? "+" : ""}${fmt(t.pointsDelta)} pts` : ""}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* REPORTES */}
        {tab === "reportes" && (
          <div>
            <section className="card" style={{ marginBottom: 14, paddingTop: 14 }}>
              <div className="row" style={{ gap: 8, flexWrap: "wrap" }}>
                <label className="muted" style={{ fontSize: 13 }}>Desde
                  <input type="date" value={reportFrom} onChange={(e) => setReportFrom(e.target.value)} style={{ marginLeft: 6, minHeight: 40 }} />
                </label>
                <label className="muted" style={{ fontSize: 13 }}>Hasta
                  <input type="date" value={reportTo} onChange={(e) => setReportTo(e.target.value)} style={{ marginLeft: 6, minHeight: 40 }} />
                </label>
                <button className="btn secondary" onClick={loadReport} style={{ width: "auto", minHeight: 40 }}>Aplicar</button>
                <button className="btn" onClick={doExport} style={{ width: "auto", minHeight: 40 }}>⬇ Exportar CSV</button>
              </div>
            </section>

            {report && (
              <>
                <div style={{ display: "grid", gap: 10, marginBottom: 14 }}>
                  {[
                    ["🧾 Ventas en el rango", fmt(report.insights.totalSales)],
                    ["👥 Miembros nuevos", fmt(report.insights.newMembers)],
                    ["🎟️ Cupones emitidos", fmt(report.insights.totalCouponsIssued)],
                    ["✅ Cupones canjeados", fmt(report.insights.totalCouponsRedeemed)],
                    ["📈 Tasa de canje", `${(report.insights.grossRedemptionRate * 100).toFixed(1)}%`],
                    ["⭐ Promedio pts/venta", fmt(Math.round(report.insights.avgPointsPerSale))],
                  ].map(([label, val]) => (
                    <div key={label} className="card row">
                      <span className="muted" style={{ fontSize: 13 }}>{label}</span>
                      <span style={{ fontWeight: 700, fontSize: 18 }}>{val}</span>
                    </div>
                  ))}
                </div>

                <section className="card" style={{ marginBottom: 14 }}>
                  <h3 style={{ fontSize: 14, margin: "0 0 10px" }}>🏬 Por tienda</h3>
                  {report.byStore.length === 0 ? <p className="muted" style={{ fontSize: 13 }}>Sin datos.</p> : (
                    <div style={{ overflowX: "auto" }}>
                      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                        <thead>
                          <tr className="muted" style={{ textAlign: "left" }}>
                            <th style={{ padding: "4px 6px" }}>Tienda</th><th>Ventas</th><th>Monto</th><th>Puntos</th><th>Canjes</th>
                          </tr>
                        </thead>
                        <tbody>
                          {report.byStore.map((s) => (
                            <tr key={s.storeId} style={{ borderTop: "1px solid var(--border)" }}>
                              <td style={{ padding: "6px" }}>{s.storeName}</td>
                              <td>{fmt(s.transactions)}</td>
                              <td>{fmt(s.amount)}</td>
                              <td>{fmt(s.pointsEarned)}</td>
                              <td>{fmt(s.couponsRedeemed)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </section>

                <section className="card">
                  <h3 style={{ fontSize: 14, margin: "0 0 10px" }}>📅 Por día</h3>
                  {report.byDay.length === 0 ? <p className="muted" style={{ fontSize: 13 }}>Sin datos.</p> : (
                    <div style={{ overflowX: "auto" }}>
                      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                        <thead>
                          <tr className="muted" style={{ textAlign: "left" }}>
                            <th style={{ padding: "4px 6px" }}>Día</th><th>Ventas</th><th>Monto</th><th>Puntos</th><th>Canjes</th>
                          </tr>
                        </thead>
                        <tbody>
                          {report.byDay.map((d) => (
                            <tr key={d.day} style={{ borderTop: "1px solid var(--border)" }}>
                              <td style={{ padding: "6px" }}>{String(d.day).slice(0, 10)}</td>
                              <td>{fmt(d.transactions)}</td>
                              <td>{fmt(d.amount)}</td>
                              <td>{fmt(d.pointsEarned)}</td>
                              <td>{fmt(d.couponsRedeemed)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </section>
              </>
            )}
          </div>
        )}
      </main>
    </div>
  );
}