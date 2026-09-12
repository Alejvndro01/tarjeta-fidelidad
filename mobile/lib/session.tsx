"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import {
  fetchMe,
  getStoredToken,
  MemberProfile,
  registerMember,
  RegisterMemberRequest,
  setStoredToken,
} from "@/lib/api";

interface SessionState {
  token: string | null;
  profile: MemberProfile | null;
  loading: boolean;
  /** Registra, guarda token y carga el perfil. Devuelve error legible o null. */
  signIn: (req: RegisterMemberRequest) => Promise<string | null>;
  refreshProfile: () => Promise<void>;
  signOut: () => void;
}

const SessionCtx = createContext<SessionState | null>(null);

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [profile, setProfile] = useState<MemberProfile | null>(null);
  const [loading, setLoading] = useState(true);

  // Hidratar sesión desde localStorage al montar.
  useEffect(() => {
    const t = getStoredToken();
    if (!t) return setLoading(false);
    setToken(t);
    fetchMe()
      .then(setProfile)
      .catch(() => setStoredToken(null)) // token inválido/expirado
      .finally(() => setLoading(false));
  }, []);

  const signIn = useCallback(async (req: RegisterMemberRequest) => {
    try {
      const res = await registerMember(req);
      setToken(res.accessToken);
      setProfile(res.member);
      return null;
    } catch (e: unknown) {
      const axios = await import("axios");
      if (axios.isAxiosError(e)) {
        const d = e.response?.data as { error?: string } | undefined;
        return d?.error ?? "No pudimos registrarte. Intenta de nuevo.";
      }
      return "Error de conexión.";
    }
  }, []);

  const refreshProfile = useCallback(async () => {
    if (!token) return;
    try {
      const p = await fetchMe();
      setProfile(p);
    } catch {
      /* sesión inválida: se maneja en signOut / próximo render */
    }
  }, [token]);

  const signOut = useCallback(() => {
    setStoredToken(null);
    setToken(null);
    setProfile(null);
  }, []);

  const value = useMemo(
    () => ({ token, profile, loading, signIn, refreshProfile, signOut }),
    [token, profile, loading, signIn, refreshProfile, signOut]
  );

  return <SessionCtx.Provider value={value}>{children}</SessionCtx.Provider>;
}

export function useSession() {
  const ctx = useContext(SessionCtx);
  if (!ctx) throw new Error("useSession debe usarse dentro de <SessionProvider>");
  return ctx;
}