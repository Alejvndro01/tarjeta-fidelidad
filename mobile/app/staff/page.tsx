"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { fetchStaffMe, getStaffToken, loginStaff } from "@/lib/api";

export default function StaffLogin() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Si ya hay sesión staff válida, ir directo al panel.
  useEffect(() => {
    if (getStaffToken()) router.replace("/staff/panel");
  }, [router]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await loginStaff(email.trim(), password);
      router.replace("/staff/panel");
    } catch (err: unknown) {
      const axios = await import("axios");
      if (axios.isAxiosError(err)) {
        const d = err.response?.data as { error?: string; message?: string } | undefined;
        setError(d?.message ?? d?.error ?? "Credenciales inválidas.");
      } else {
        setError("Error de conexión.");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="app-root" style={{ paddingBottom: 0 }}>
      <main className="app-main" style={{ alignSelf: "center", width: "100%", maxWidth: 420, paddingTop: 48 }}>
        <div className="center" style={{ marginBottom: 24 }}>
          <div style={{ fontSize: 40 }}>🛒</div>
          <h1 style={{ margin: "8px 0 2px", fontSize: 22 }}>Panel de cajero</h1>
          <p className="muted" style={{ fontSize: 14, margin: 0 }}>Inicia sesión para canjear cupones</p>
        </div>
        <form onSubmit={onSubmit}>
          <div className="field">
            <label htmlFor="email">Correo</label>
            <input id="email" type="email" autoComplete="username" value={email}
              onChange={(e) => setEmail(e.target.value)} placeholder="cajero@burger.dev" required />
          </div>
          <div className="field">
            <label htmlFor="pwd">Contraseña</label>
            <input id="pwd" type="password" autoComplete="current-password" value={password}
              onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" required />
          </div>
          {error && <p className="center" style={{ color: "var(--danger)", fontSize: 14 }}>{error}</p>}
          <button className="btn" type="submit" disabled={loading} style={{ marginTop: 8 }}>
            {loading ? "Entrando…" : "Entrar"}
          </button>
        </form>

        <p className="muted center" style={{ fontSize: 12, marginTop: 16 }}>
          Demo: <code>cajero@burger.dev</code>
        </p>
      </main>
    </div>
  );
}