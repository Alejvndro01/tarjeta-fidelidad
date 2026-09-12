"use client";

import { useSession } from "@/lib/session";
import { useRouter } from "next/navigation";

export default function AjustesPage() {
  const { profile, signOut } = useSession();
  const router = useRouter();

  function logout() {
    signOut();
    router.replace("/app/register");
  }

  return (
    <div>
      <h1 style={{ marginTop: 4 }}>Ajustes</h1>
      <section className="card">
        <div className="row" style={{ marginBottom: 10 }}>
          <span className="muted">Nombre</span>
          <span>{profile?.fullName}</span>
        </div>
        <div className="row" style={{ marginBottom: 10 }}>
          <span className="muted">Celular</span>
          <span>{profile?.phoneNumber}</span>
        </div>
        <div className="row" style={{ marginBottom: 16 }}>
          <span className="muted">Marca</span>
          <span>{profile?.tenantSlug}</span>
        </div>
        <button className="btn ghost" onClick={logout}>Cerrar sesión</button>
      </section>

      <p className="muted center" style={{ fontSize: 11, marginTop: 18 }}>
        Tarjeta Fidelidad · v0.4 · Sin conexión parcial habilitada
      </p>
    </div>
  );
}