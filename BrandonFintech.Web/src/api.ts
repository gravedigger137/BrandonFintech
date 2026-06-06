import { getToken } from "./auth";
import type { Account, ApiEnvelope, DashboardSummary, Payment, Transaction, Transfer, User } from "./types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5015";

type RequestOptions = {
  method?: "GET" | "POST";
  body?: unknown;
  auth?: boolean;
  idempotencyKey?: string;
};

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<ApiEnvelope<T>> {
  const headers = new Headers();
  headers.set("Accept", "application/json");

  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (options.auth ?? true) {
    const token = getToken();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }
  }

  if (options.idempotencyKey) {
    headers.set("Idempotency-Key", options.idempotencyKey);
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body)
    });
  } catch (error) {
    throw new ApiError(
      error instanceof Error ? error.message : "Unable to reach the API",
      0
    );
  }

  const data = await readResponseBody(response);

  if (!response.ok) {
    const message = typeof data.message === "string" ? data.message : `Request failed with ${response.status}`;
    throw new ApiError(message, response.status, data);
  }

  return data as ApiEnvelope<T>;
}

export async function downloadCsv(path: string): Promise<Blob> {
  const headers = new Headers();
  const token = getToken();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, { headers });
  if (!response.ok) {
    throw new ApiError(`CSV export failed with ${response.status}`, response.status);
  }

  return response.blob();
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly details?: unknown
  ) {
    super(message);
  }
}

type DashboardSummaryEnvelope =
  | ({ summary: DashboardSummary } & ApiEnvelope<Record<string, never>>)
  | ApiEnvelope<DashboardSummary>;

export const api = {
  login: (email: string, password: string) =>
    apiRequest<{ accessToken: string; user: User }>("/api/v1/auth/login", {
      method: "POST",
      auth: false,
      body: { email, password }
    }),
  register: (email: string, password: string, firstName: string, lastName: string) =>
    apiRequest<{ user: User; defaultAccount: Account }>("/api/v1/auth/register", {
      method: "POST",
      auth: false,
      body: { email, password, firstName, lastName }
    }),
  me: () => apiRequest<{ user: User }>("/api/v1/auth/me"),
  dashboard: async () => {
    try {
      const response = await apiRequest<DashboardSummaryEnvelope>("/api/v1/dashboard/summary");
      return normalizeDashboardResponse(response);
    } catch (error) {
      return buildDashboardFallback(error);
    }
  },
  accounts: () => apiRequest<{ accounts: Account[] }>("/api/v1/accounts"),
  createAccount: () =>
    apiRequest<{ account: Account }>("/api/v1/accounts", {
      method: "POST",
      body: {},
      idempotencyKey: crypto.randomUUID()
    }),
  deposit: (accountId: string, amount: number, description: string) =>
    apiRequest<{ account: Account }>(`/api/v1/accounts/${accountId}/deposit`, {
      method: "POST",
      body: { amount, description },
      idempotencyKey: crypto.randomUUID()
    }),
  transfer: (fromAccountId: string, toAccountId: string, amount: number, description: string) =>
    apiRequest<{ transfer: Transfer }>("/api/v1/transfers/internal", {
      method: "POST",
      body: { fromAccountId, toAccountId, amount, description },
      idempotencyKey: crypto.randomUUID()
    }),
  payments: () => apiRequest<{ payments: Payment[] }>("/api/v1/payments"),
  createPaymentIntent: (amount: number, currency: string) =>
    apiRequest<{ payment: Payment; clientSecret: string }>("/api/v1/payments/intents", {
      method: "POST",
      body: { amount, currency },
      idempotencyKey: crypto.randomUUID()
    }),
  transactions: () => apiRequest<{ transactions: Transaction[] }>("/api/v1/transactions"),
  transfers: () => apiRequest<{ transfers: Transfer[] }>("/api/v1/transfers"),
  adminUsers: () => apiRequest<{ users: User[] }>("/api/v1/admin/users"),
  adminAccounts: () => apiRequest<{ accounts: Account[] }>("/api/v1/admin/accounts"),
  adminPayments: () => apiRequest<{ payments: Payment[] }>("/api/v1/admin/payments"),
  adminTransfers: () => apiRequest<{ transfers: Transfer[] }>("/api/v1/admin/transfers"),
  adminAuditLogs: () =>
    apiRequest<{ auditLogs: Array<{ id: string; action: string; entityType: string; entityId: string; createdAt: string }> }>(
      "/api/v1/admin/audit-logs"
    ),
  releasePromoCredit: (accountId: string) =>
    apiRequest<{ account: Account }>(`/api/v1/admin/accounts/${accountId}/release-promo-credit`, {
      method: "POST"
    })
};

async function readResponseBody(response: Response): Promise<Record<string, unknown>> {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    try {
      return await response.json() as Record<string, unknown>;
    } catch {
      return { success: response.ok, message: "API returned invalid JSON" };
    }
  }

  return { success: response.ok, message: await response.text() };
}

function normalizeDashboardResponse(response: DashboardSummaryEnvelope): ApiEnvelope<DashboardSummary> {
  const candidate = "summary" in response ? response.summary : response;

  return {
    success: response.success,
    totalAccounts: toNumber(candidate.totalAccounts),
    totalAvailableBalance: toNumber(candidate.totalAvailableBalance),
    totalPayments: toNumber(candidate.totalPayments),
    totalTransfers: toNumber(candidate.totalTransfers),
    recentPayments: Array.isArray(candidate.recentPayments) ? candidate.recentPayments : [],
    recentTransfers: Array.isArray(candidate.recentTransfers) ? candidate.recentTransfers : []
  };
}

async function buildDashboardFallback(error: unknown): Promise<ApiEnvelope<DashboardSummary>> {
  const [accountsResult, paymentsResult, transfersResult] = await Promise.allSettled([
    api.accounts(),
    api.payments(),
    api.transfers()
  ]);

  const accounts = accountsResult.status === "fulfilled" ? accountsResult.value.accounts : [];
  const payments = paymentsResult.status === "fulfilled" ? paymentsResult.value.payments : [];
  const transfers = transfersResult.status === "fulfilled" ? transfersResult.value.transfers : [];

  if (accountsResult.status === "rejected" && paymentsResult.status === "rejected" && transfersResult.status === "rejected") {
    throw error;
  }

  return {
    success: true,
    message: "Dashboard summary endpoint failed; showing partial data from available endpoints.",
    totalAccounts: accounts.length,
    totalAvailableBalance: accounts.reduce((total, account) => total + toNumber(account.availableBalance), 0),
    totalPayments: payments.length,
    totalTransfers: transfers.length,
    recentPayments: payments.slice(0, 5),
    recentTransfers: transfers.slice(0, 5)
  };
}

function toNumber(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}
