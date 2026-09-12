"use client";

import axios from "axios";

export const API_BASE =
  process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:5080";

/**
 * Cliente HTTP tipado para el backend.
 * POR QUÉ axios y no fetch directo: interceptor central de tokens (Bearer + refresh) y
 * parseo uniforme de errores de negocio del middleware (409/401/429). Esto se comparte
 * entre el registro/login móvil y las llamadas autenticadas.
 */
export const api = axios.create({ baseURL: API_BASE, timeout: 15000 });

export interface MemberProfile {
  memberId: string;
  phoneNumber: string;
  fullName: string;
  email?: string | null;
  pointsBalance: number;
  qrHash: string;
  tenantSlug: string;
  stamps: StampProgress[];
}

export interface StampProgress {
  productSku: string;
  productName: string;
  count: number;
  stampsRequired: number;
  completed: boolean;
}

export interface RegisterMemberRequest {
  phoneNumber: string;
  fullName: string;
  email?: string;
  tenantSlug: string;
}

export interface RegisterMemberResponse {
  accessToken: string;
  accessTokenExpiresInSeconds: number;
  member: MemberProfile;
}

// --- Lectura del token de sesión desde localStorage (cliente) ---
const TOKEN_KEY = "loyalty.member.token";

export function getStoredToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}
export function setStoredToken(t: string | null) {
  if (typeof window === "undefined") return;
  if (t) window.localStorage.setItem(TOKEN_KEY, t);
  else window.localStorage.removeItem(TOKEN_KEY);
}

// Interceptor: adjunta Bearer si existe
api.interceptors.request.use((config) => {
  const t = getStoredToken();
  if (t) config.headers.Authorization = `Bearer ${t}`;
  return config;
});

export async function registerMember(req: RegisterMemberRequest): Promise<RegisterMemberResponse> {
  const { data } = await api.post<RegisterMemberResponse>("/api/members/register", req);
  setStoredToken(data.accessToken);
  return data;
}

export async function fetchMe(): Promise<MemberProfile> {
  const { data } = await api.get<MemberProfile>("/api/members/me");
  return data;
}

// --- Wallet ---

export interface WalletPassResult {
  provider: string;
  signedJwt?: string;
  googleAddUrl?: string;
}

/** Pide el JWT de Google Wallet y devuelve la URL "Añadir a la cartera". */
export async function fetchGooglePass(): Promise<string | null> {
  const { data } = await api.get<WalletPassResult>("/api/wallet/google");
  return data.googleAddUrl ?? (data.signedJwt ? `https://pay.google.com/gp/v/save/${data.signedJwt}` : null);
}

/** Descarga el .pkpass (Apple Wallet) como archivo de instalación. */
export async function downloadApplePass(): Promise<boolean> {
  const token = getStoredToken();
  if (!token) return false;
  const resp = await fetch(`${API_BASE}/api/wallet/apple`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok) return false;
  const blob = await resp.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "fidelidad.pkpass";
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
  return true;
}

export function apiErrorMessage(e: unknown): string {
  if (axios.isAxiosError(e)) {
    const d = e.response?.data as { error?: string; message?: string } | undefined;
    return d?.message ?? d?.error ?? (e.response?.status === 401 ? "Sesión expirada. Vuelve a entrar." : "Error de conexión.");
  }
  return "Algo salió mal.";
}

// --- Staff (panel de cajero) ---
// Instancia axios SEPARADA del api (cliente de miembro) para no heredar el interceptor
// que inyecta el Bearer del consumidor a todas las peticiones.
export const staffApi = axios.create({ baseURL: API_BASE, timeout: 15000 });

const STAFF_TOKEN_KEY = "loyalty.staff.token";

export interface StaffLoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresInSeconds: number;
  refreshTokenExpiresInSeconds: number;
}

export interface StaffMe {
  user: { id: string; email: string; role: string; tenantId: string | null; storeId: string | null };
}

export function getStaffToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(STAFF_TOKEN_KEY);
}
export function setStaffToken(t: string | null) {
  if (typeof window === "undefined") return;
  if (t) window.localStorage.setItem(STAFF_TOKEN_KEY, t);
  else window.localStorage.removeItem(STAFF_TOKEN_KEY);
}

export async function loginStaff(email: string, password: string): Promise<StaffLoginResponse> {
  const { data } = await staffApi.post<StaffLoginResponse>("/api/auth/login", { email, password });
  setStaffToken(data.accessToken);
  return data;
}

export async function fetchStaffMe(): Promise<StaffMe> {
  const t = getStaffToken();
  const { data } = await staffApi.get<StaffMe>("/api/auth/me", { headers: { Authorization: `Bearer ${t}` } });
  return data;
}

export interface RedeemRequest {
  couponCode: string;
  storeId: string;
}
export interface RedeemResult {
  couponId: string;
  couponCode: string;
  title: string;
  redeemed: boolean;
  message: string;
}
export interface RedeemedRecord {
  couponId: string;
  couponCode: string;
  title: string;
  redeemedUtc: string;
  redeemedByEmail?: string;
  storeName?: string;
}
export interface StoreOption {
  storeId: string;
  name: string;
}

/** Canjea un cupón (staff). Devuelve el resultado o lanza con mensaje del servidor. */
export async function redeemCoupon(req: RedeemRequest): Promise<RedeemResult> {
  const t = getStaffToken();
  const { data } = await staffApi.post<RedeemResult>("/api/pos/coupons/redeem", req, { headers: { Authorization: `Bearer ${t}` } });
  return data;
}
export async function fetchRedeemed(take = 25): Promise<RedeemedRecord[]> {
  const t = getStaffToken();
  const { data } = await staffApi.get<RedeemedRecord[]>("/api/pos/coupons/redeemed", { params: { take }, headers: { Authorization: `Bearer ${t}` } });
  return data;
}
export async function fetchStores(): Promise<StoreOption[]> {
  const t = getStaffToken();
  const { data } = await staffApi.get<StoreOption[]>("/api/pos/stores", { headers: { Authorization: `Bearer ${t}` } });
  return data;
}

// --- Admin (panel de administración: TenantAdmin/SuperAdmin) ---
// Instancia axios separada con su propio token (no hereda el interceptor del miembro).
export const adminApi = axios.create({ baseURL: API_BASE, timeout: 15000 });

const ADMIN_TOKEN_KEY = "loyalty.admin.token";

export function getAdminToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(ADMIN_TOKEN_KEY);
}
export function setAdminToken(t: string | null) {
  if (typeof window === "undefined") return;
  if (t) window.localStorage.setItem(ADMIN_TOKEN_KEY, t);
  else window.localStorage.removeItem(ADMIN_TOKEN_KEY);
}

function adminHeaders() {
  const t = getAdminToken();
  return { Authorization: `Bearer ${t}` };
}

export async function loginAdmin(email: string, password: string): Promise<StaffLoginResponse> {
  const { data } = await adminApi.post<StaffLoginResponse>("/api/auth/login", { email, password });
  setAdminToken(data.accessToken);
  return data;
}

export interface AdminDashboard {
  memberCount: number;
  activeCoupons: number;
  redeemedCoupons: number;
  totalPointsIssued: number;
  totalPointsRedeemed: number;
}

export interface AdminMember {
  memberId: string;
  qrHash: string;
  phoneNumber: string;
  fullName: string;
  email?: string | null;
  pointsBalance: number;
  joinedAtUtc: string;
}

export interface AdminCoupon {
  couponId: string;
  couponCode: string;
  title: string;
  status: string;
  memberId: string;
  memberName?: string;
  memberPhone?: string;
  issuedUtc: string;
  expiresUtc?: string | null;
  redeemedUtc?: string | null;
  storeName?: string;
}

export interface AdminStampRule { ruleId: string; productSku: string; productName: string; stampsRequired: number; }
export interface AdminReward { rewardId: string; name: string; description?: string; pointsCost: number; stampCost: number; isActive: boolean; }
export interface AdminProgram {
  programId: string;
  name: string;
  pointsEnabled: boolean;
  stampsEnabled: boolean;
  pointsPerMonetaryUnit: number;
  currencyCode: string;
  stampRules: AdminStampRule[];
  rewards: AdminReward[];
}

export interface AdminTransaction {
  transactionId: string;
  memberId: string;
  memberName?: string;
  type: string;
  pointsDelta: number;
  stampsDelta: number;
  amount?: number | null;
  reference: string;
  storeName?: string;
  createdUtc: string;
}

export interface CreateCouponResult { couponId: string; couponCode: string; title: string; expiresUtc?: string | null; }

export async function fetchAdminDashboard(): Promise<AdminDashboard> {
  const { data } = await adminApi.get<AdminDashboard>("/api/admin/dashboard", { headers: adminHeaders() });
  return data;
}
export async function fetchAdminMembers(page = 1, pageSize = 50, search = ""): Promise<{ items: AdminMember[]; total: number }> {
  const resp = await adminApi.get<AdminMember[]>("/api/admin/members", { headers: adminHeaders(), params: { page, pageSize, search } });
  return { items: resp.data, total: Number(resp.headers["x-total-count"] ?? 0) };
}
export async function fetchAdminCoupons(page = 1, pageSize = 50): Promise<{ items: AdminCoupon[]; total: number }> {
  const resp = await adminApi.get<AdminCoupon[]>("/api/admin/coupons", { headers: adminHeaders(), params: { page, pageSize } });
  return { items: resp.data, total: Number(resp.headers["x-total-count"] ?? 0) };
}
export async function fetchAdminProgram(): Promise<AdminProgram> {
  const { data } = await adminApi.get<AdminProgram>("/api/admin/program", { headers: adminHeaders() });
  return data;
}
export async function updateProgramPoints(req: { pointsEnabled: boolean; stampsEnabled: boolean; pointsPerMonetaryUnit: number }): Promise<void> {
  await adminApi.put("/api/admin/program/points", req, { headers: adminHeaders() });
}
export async function addStampRule(req: { productSku: string; productName: string; stampsRequired: number }): Promise<void> {
  await adminApi.post("/api/admin/program/stamp-rules", req, { headers: adminHeaders() });
}
export async function addReward(req: { name: string; description?: string; pointsCost: number; stampCost: number }): Promise<void> {
  await adminApi.post("/api/admin/program/rewards", req, { headers: adminHeaders() });
}
export async function createCoupon(req: { memberId: string; title: string; description?: string; expiresUtc?: string }): Promise<CreateCouponResult> {
  const { data } = await adminApi.post<CreateCouponResult>("/api/admin/coupons", req, { headers: adminHeaders() });
  return data;
}
export async function fetchAdminTransactions(page = 1, pageSize = 50): Promise<{ items: AdminTransaction[]; total: number }> {
  const resp = await adminApi.get<AdminTransaction[]>("/api/admin/transactions", { headers: adminHeaders(), params: { page, pageSize } });
  return { items: resp.data, total: Number(resp.headers["x-total-count"] ?? 0) };
}

// --- Fase 10: Reportes ---

export interface AdminStoreReport {
  storeId: string;
  storeName: string;
  transactions: number;
  pointsEarned: number;
  amount: number;
  couponsIssued: number;
  couponsRedeemed: number;
}
export interface AdminDailyReport {
  day: string;
  transactions: number;
  amount: number;
  pointsEarned: number;
  couponsRedeemed: number;
}
export interface AdminInsights {
  newMembers: number;
  activeRedemptions: number;
  grossRedemptionRate: number;
  avgPointsPerSale: number;
  totalSales: number;
  totalCouponsIssued: number;
  totalCouponsRedeemed: number;
  totalNewMembers: number;
}
export interface AdminReport {
  insights: AdminInsights;
  byStore: AdminStoreReport[];
  byDay: AdminDailyReport[];
}

export async function fetchAdminReport(from: string, to: string): Promise<AdminReport> {
  const { data } = await adminApi.get<AdminReport>("/api/admin/report", { headers: adminHeaders(), params: { from, to } });
  return data;
}
export async function exportReportCsv(from: string, to: string): Promise<void> {
  const t = getAdminToken();
  const resp = await fetch(`${API_BASE}/api/admin/report/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`, {
    headers: { Authorization: `Bearer ${t}` },
  });
  if (!resp.ok) throw new Error("No se pudo exportar el reporte.");
  const blob = await resp.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `reporte-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}