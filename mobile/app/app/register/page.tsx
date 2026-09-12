"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useSession } from "@/lib/session";

const TENANT = "burger-demo"; // slug del tenant demo (cada marca tiene su slug)

export default function RegisterPage() {
  const { signIn, loading } = useSession();
  const router = useRouter();
  const [phone, setPhone] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const err = await signIn({
      phoneNumber: phone.trim(),
      fullName: name.trim(),
      email: email.trim() || undefined,
      tenantSlug: TENANT,
    });
    setSubmitting(false);
    if (err) return setError(err);
    router.replace("/app");
  }

  return (
    <div style={{ paddingTop: 32 }}>
      <div className="center" style={{ marginBottom: 28 }}>
        <div style={{ fontSize: 44 }}>🎁</div>
        <h1 style={{ margin: "8px 0 4px" }}>Tu tarjeta fidelidad</h1>
        <p className="muted" style={{ fontSize: 14, margin: 0 }}>
          Únete con tu celular. Solo necesitas tu número y nombre.
        </p>
      </div>

      <form onSubmit={onSubmit}>
        <div className="field">
          <label htmlFor="phone">Número de celular</label>
          <input
            id="phone"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            placeholder="+56 9 1234 5678"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="name">Nombre</label>
          <input
            id="name"
            type="text"
            autoComplete="name"
            placeholder="Tu nombre"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="email">Correo (opcional)</label>
          <input
            id="email"
            type="email"
            inputMode="email"
            autoComplete="email"
            placeholder="tucorreo@ejemplo.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>

        {error && <p className="center" style={{ color: "var(--danger)", fontSize: 14 }}>{error}</p>}

        <button className="btn" type="submit" disabled={submitting || loading} style={{ marginTop: 8 }}>
          {submitting ? "Guardando…" : "Obtener mi tarjeta"}
        </button>
      </form>

      <p className="center muted" style={{ fontSize: 12, marginTop: 20 }}>
        Al unirte aceptas el programa de fidelidad de esta marca.
        <br />
        <Link href="/app/ajustes" style={{ color: "var(--accent)" }}>Ya tengo cuenta</Link>
      </p>
    </div>
  );
}