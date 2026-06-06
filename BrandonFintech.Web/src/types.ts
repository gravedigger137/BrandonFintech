export type User = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role?: string;
};

export type Account = {
  id: string;
  userId: string;
  accountNumber: string;
  availableBalance: number;
  pendingBalance: number;
  isActive: boolean;
  createdAt: string;
};

export type Payment = {
  id: string;
  userId: string;
  amount: number;
  currency: string;
  stripePaymentIntentId: string;
  status: string;
  createdAt: string;
};

export type Transfer = {
  id: string;
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  status: string;
  createdAt: string;
};

export type DashboardSummary = {
  totalAccounts: number;
  totalAvailableBalance: number;
  totalPayments: number;
  totalTransfers: number;
  recentPayments: Payment[];
  recentTransfers: Transfer[];
};

export type Transaction = {
  id: string;
  type: string;
  description: string;
  amount: number;
  createdAt: string;
  accountId?: string;
  fromAccountId?: string;
  toAccountId?: string;
};

export type ApiEnvelope<T> = T & {
  success: boolean;
  message?: string;
};
