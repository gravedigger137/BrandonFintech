import { Component, ErrorInfo, FormEvent, ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, NavLink, Route, Routes, useNavigate } from "react-router-dom";
import { api, downloadCsv } from "./api";
import { clearToken, getToken, isAdmin, setToken } from "./auth";
import type { Account, DashboardSummary, Payment, Transaction, Transfer, User } from "./types";

type LoadState<T> = {
  data: T | null;
  loading: boolean;
  error: string | null;
};

const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD"
});

function App() {
  const [token, setTokenState] = useState<string | null>(() => getToken());
  const [currentUser, setCurrentUser] = useState<User | null>(null);

  useEffect(() => {
    if (!token) {
      setCurrentUser(null);
      return;
    }

    api.me()
      .then((response) => setCurrentUser(response.user))
      .catch(() => {
        clearToken();
        setTokenState(null);
      });
  }, [token]);

  function handleLogin(accessToken: string) {
    setToken(accessToken);
    setTokenState(accessToken);
  }

  function logout() {
    clearToken();
    setTokenState(null);
  }

  return (
    <AppErrorBoundary>
      <Routes>
        <Route path="/login" element={<LoginPage onLogin={handleLogin} />} />
        <Route path="/register" element={<RegisterPage onLogin={handleLogin} />} />
        <Route
          path="/*"
          element={
            <ProtectedRoute isAuthenticated={Boolean(token)}>
              <Shell user={currentUser} onLogout={logout} />
            </ProtectedRoute>
          }
        />
      </Routes>
    </AppErrorBoundary>
  );
}

function ProtectedRoute({ isAuthenticated, children }: { isAuthenticated: boolean; children: ReactNode }) {
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

function Shell({ user, onLogout }: { user: User | null; onLogout: () => void }) {
  const admin = isAdmin();
  const navigate = useNavigate();

  function logout() {
    onLogout();
    navigate("/login");
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <Link to="/" className="brand">
          <span className="brand-mark">B</span>
          <span>BrandonFintech</span>
        </Link>
        <nav>
          <NavLink to="/">Dashboard</NavLink>
          <NavLink to="/accounts">Accounts</NavLink>
          <NavLink to="/transfer">Transfers</NavLink>
          <NavLink to="/payments">Payments</NavLink>
          <NavLink to="/transactions">Transactions</NavLink>
          {admin && <NavLink to="/admin">Admin</NavLink>}
        </nav>
        <div className="sidebar-footer">
          <span className="nav-label">Signed in as</span>
          <span>{user?.email ?? "Loading profile..."}</span>
          {admin && <span className="badge">Admin</span>}
          <button type="button" className="secondary-button" onClick={logout}>Log out</button>
        </div>
      </aside>
      <main className="main">
        <Routes>
          <Route index element={<DashboardPage />} />
          <Route path="accounts" element={<AccountsPage />} />
          <Route path="transfer" element={<TransferPage />} />
          <Route path="payments" element={<PaymentsPage />} />
          <Route path="transactions" element={<TransactionsPage />} />
          <Route path="admin" element={admin ? <AdminPage /> : <Navigate to="/" replace />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

function LoginPage({ onLogin }: { onLogin: (token: string) => void }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busyRef.current) {
      return;
    }

    busyRef.current = true;
    setBusy(true);
    setError(null);

    try {
      const response = await api.login(email, password);
      onLogin(response.accessToken);
      navigate("/", { replace: true });
    } catch (err) {
      setError(toMessage(err));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }

  return (
    <AuthLayout title="Sign in">
      <form onSubmit={submit} className="form">
        <Field label="Email" value={email} onChange={setEmail} type="email" />
        <Field label="Password" value={password} onChange={setPassword} type="password" />
        {error && <p className="error">{error}</p>}
        <button disabled={busy}>{busy ? "Signing in..." : "Sign in"}</button>
      </form>
      <p className="muted">Need an account? <Link to="/register">Register</Link></p>
    </AuthLayout>
  );
}

function RegisterPage({ onLogin }: { onLogin: (token: string) => void }) {
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busyRef.current) {
      return;
    }

    busyRef.current = true;
    setBusy(true);
    setError(null);

    try {
      await api.register(email, password, firstName, lastName);
      const login = await api.login(email, password);
      onLogin(login.accessToken);
      navigate("/", { replace: true });
    } catch (err) {
      setError(toMessage(err));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }

  return (
    <AuthLayout title="Create account">
      <form onSubmit={submit} className="form">
        <div className="grid two">
          <Field label="First name" value={firstName} onChange={setFirstName} />
          <Field label="Last name" value={lastName} onChange={setLastName} />
        </div>
        <Field label="Email" value={email} onChange={setEmail} type="email" />
        <Field label="Password" value={password} onChange={setPassword} type="password" />
        {error && <p className="error">{error}</p>}
        <button disabled={busy}>{busy ? "Creating..." : "Register"}</button>
      </form>
      <p className="muted">Already registered? <Link to="/login">Sign in</Link></p>
    </AuthLayout>
  );
}

function AuthLayout({ title, children }: { title: string; children: ReactNode }) {
  return (
    <main className="auth-page">
      <section className="auth-panel">
        <Link to="/login" className="auth-brand">
          <span className="brand-mark">B</span>
          <span>BrandonFintech</span>
        </Link>
        <h1>{title}</h1>
        {children}
      </section>
    </main>
  );
}

function DashboardPage() {
  const state = useLoader(() => api.dashboard(), []);
  const accountsState = useLoader(() => api.accounts(), []);
  const summary = state.data;
  const accounts = accountsState.data?.accounts ?? [];
  const pendingBalance = accounts.reduce((total, account) => total + (account.pendingBalance ?? 0), 0);

  return (
    <Page title="Dashboard" state={state}>
      {summary && (
        <>
          {summary.message && <Notice notice={{ kind: "error", text: summary.message }} />}
          <section className="dashboard-hero">
            <div>
              <span className="eyebrow">Portfolio overview</span>
              <h2>{formatMoney(summary.totalAvailableBalance)} available</h2>
              <p>{accounts.length} active account{accounts.length === 1 ? "" : "s"} tracked in BrandonFintech.</p>
            </div>
            <div className="hero-balance">
              <span>Pending review</span>
              <strong>{formatMoney(pendingBalance)}</strong>
            </div>
          </section>
          <div className="metrics">
            <Metric label="Accounts" value={summary.totalAccounts ?? 0} tone="blue" />
            <Metric label="Available balance" value={formatMoney(summary.totalAvailableBalance)} tone="green" />
            <Metric label="Pending balance" value={formatMoney(pendingBalance)} tone="amber" />
            <Metric label="Payments" value={summary.totalPayments ?? 0} tone="violet" />
            <Metric label="Transfers" value={summary.totalTransfers ?? 0} tone="slate" />
          </div>
          <div className="grid three">
            <VisualPanel title="Balance trend" caption="Placeholder for settled and pending balance movement." variant="trend" />
            <VisualPanel title="Payments summary" caption="Placeholder for Stripe test payment outcomes." variant="bars" />
            <VisualPanel title="Transfer activity" caption="Placeholder for debit and credit flow volume." variant="activity" />
          </div>
          <div className="grid two">
            <Panel title="Recent payments">
              <PaymentTable payments={summary.recentPayments ?? []} />
            </Panel>
            <Panel title="Recent transfers">
              <TransferTable transfers={summary.recentTransfers ?? []} />
            </Panel>
          </div>
        </>
      )}
    </Page>
  );
}

function AccountsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useLoader(() => api.accounts(), [refreshKey]);
  const accounts = state.data?.accounts ?? [];
  const [selectedAccountId, setSelectedAccountId] = useState("");
  const [depositAmount, setDepositAmount] = useState("25.00");
  const [depositDescription, setDepositDescription] = useState("Browser demo deposit");
  const [notice, setNotice] = useState<NoticeState>(null);
  const [creatingAccount, setCreatingAccount] = useState(false);
  const [depositing, setDepositing] = useState(false);
  const creatingAccountRef = useRef(false);
  const depositingRef = useRef(false);

  useEffect(() => {
    if (!selectedAccountId && accounts.length > 0) {
      setSelectedAccountId(accounts[0].id);
    }
  }, [accounts, selectedAccountId]);

  async function createAccount() {
    if (creatingAccountRef.current) {
      return;
    }

    creatingAccountRef.current = true;
    setNotice(null);
    setCreatingAccount(true);
    try {
      await api.createAccount();
      setRefreshKey((value) => value + 1);
      setNotice({ kind: "success", text: "Account created." });
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    } finally {
      creatingAccountRef.current = false;
      setCreatingAccount(false);
    }
  }

  async function deposit(event: FormEvent) {
    event.preventDefault();
    if (depositingRef.current) {
      return;
    }

    depositingRef.current = true;
    setNotice(null);
    setDepositing(true);
    try {
      await api.deposit(selectedAccountId, Number(depositAmount), depositDescription);
      setRefreshKey((value) => value + 1);
      setNotice({ kind: "success", text: "Deposit posted." });
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    } finally {
      depositingRef.current = false;
      setDepositing(false);
    }
  }

  async function exportStatement(accountId: string) {
    try {
      const blob = await downloadCsv(`/api/v1/accounts/${accountId}/statement.csv`);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `statement-${accountId}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    }
  }

  return (
    <Page title="Accounts" state={state}>
      <div className="actions">
        <button type="button" onClick={createAccount} disabled={creatingAccount}>
          {creatingAccount ? "Creating..." : "Create account"}
        </button>
        <Notice notice={notice} />
      </div>
      <div className="account-card-grid">
        {accounts.slice(0, 3).map((account) => (
          <article className="account-summary-card" key={account.id}>
            <span>{account.accountNumber}</span>
            <strong>{formatMoney(account.availableBalance)}</strong>
            <small>Pending {formatMoney(account.pendingBalance)}</small>
          </article>
        ))}
      </div>
      <AccountTable accounts={accounts} onExport={exportStatement} />
      <Panel title="Deposit funds">
        <form onSubmit={deposit} className="form inline-form">
          <Select label="Account" value={selectedAccountId} onChange={setSelectedAccountId} options={accounts.map(accountOption)} />
          <Field label="Amount" value={depositAmount} onChange={setDepositAmount} type="number" step="0.01" />
          <Field label="Description" value={depositDescription} onChange={setDepositDescription} />
          <button disabled={!selectedAccountId || depositing}>{depositing ? "Depositing..." : "Deposit"}</button>
        </form>
      </Panel>
    </Page>
  );
}

function TransferPage() {
  const accountsState = useLoader(() => api.accounts(), []);
  const accounts = accountsState.data?.accounts ?? [];
  const [fromAccountId, setFromAccountId] = useState("");
  const [toAccountId, setToAccountId] = useState("");
  const [amount, setAmount] = useState("10.00");
  const [description, setDescription] = useState("Browser demo transfer");
  const [notice, setNotice] = useState<NoticeState>(null);
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);

  useEffect(() => {
    if (accounts.length > 0 && !fromAccountId) {
      setFromAccountId(accounts[0].id);
    }
    if (accounts.length > 1 && !toAccountId) {
      setToAccountId(accounts[1].id);
    }
  }, [accounts, fromAccountId, toAccountId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submittingRef.current) {
      return;
    }

    submittingRef.current = true;
    setNotice(null);
    setSubmitting(true);
    try {
      const response = await api.transfer(fromAccountId, toAccountId, Number(amount), description);
      setNotice({ kind: "success", text: `Transfer completed: ${response.transfer.id}` });
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  return (
    <Page title="Internal transfer" state={accountsState}>
      <Panel title="Move money between accounts">
        <form onSubmit={submit} className="form">
          <Select label="From account" value={fromAccountId} onChange={setFromAccountId} options={accounts.map(accountOption)} />
          <Select label="To account" value={toAccountId} onChange={setToAccountId} options={accounts.map(accountOption)} />
          <Field label="Amount" value={amount} onChange={setAmount} type="number" step="0.01" />
          <Field label="Description" value={description} onChange={setDescription} />
          <Notice notice={notice} />
          <button disabled={!fromAccountId || !toAccountId || submitting}>
            {submitting ? "Submitting..." : "Submit transfer"}
          </button>
        </form>
      </Panel>
    </Page>
  );
}

function PaymentsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useLoader(() => api.payments(), [refreshKey]);
  const [amount, setAmount] = useState("10.00");
  const [currency, setCurrency] = useState("USD");
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [notice, setNotice] = useState<NoticeState>(null);
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submittingRef.current) {
      return;
    }

    submittingRef.current = true;
    setNotice(null);
    setClientSecret(null);
    setSubmitting(true);
    try {
      const response = await api.createPaymentIntent(Number(amount), currency);
      setClientSecret(response.clientSecret);
      setRefreshKey((value) => value + 1);
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  return (
    <Page title="Payments" state={state}>
      <Panel title="Create Stripe PaymentIntent">
        <form onSubmit={submit} className="form inline-form">
          <Field label="Amount" value={amount} onChange={setAmount} type="number" step="0.01" />
          <Field label="Currency" value={currency} onChange={setCurrency} />
          <button disabled={submitting}>{submitting ? "Creating..." : "Create intent"}</button>
        </form>
        {clientSecret && <p className="code-line">client_secret: {clientSecret}</p>}
        <Notice notice={notice} />
      </Panel>
      <PaymentTable payments={state.data?.payments ?? []} />
    </Page>
  );
}

function TransactionsPage() {
  const state = useLoader(() => api.transactions(), []);

  return (
    <Page title="Transaction history" state={state}>
      <table>
        <thead>
          <tr>
            <th>Date</th>
            <th>Type</th>
            <th>Description</th>
            <th>Amount</th>
          </tr>
        </thead>
        <tbody>
          {(state.data?.transactions ?? []).map((transaction) => (
            <tr key={`${transaction.type}-${transaction.id}`}>
              <td>{formatDate(transaction.createdAt)}</td>
              <td>{transaction.type}</td>
              <td>{transaction.description}</td>
              <td>{formatMoney(transaction.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Page>
  );
}

function AdminPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const users = useLoader(() => api.adminUsers(), [refreshKey]);
  const accounts = useLoader(() => api.adminAccounts(), [refreshKey]);
  const payments = useLoader(() => api.adminPayments(), [refreshKey]);
  const transfers = useLoader(() => api.adminTransfers(), [refreshKey]);
  const audit = useLoader(() => api.adminAuditLogs(), [refreshKey]);
  const [notice, setNotice] = useState<NoticeState>(null);
  const [releasingAccountId, setReleasingAccountId] = useState<string | null>(null);
  const releasingAccountIdsRef = useRef(new Set<string>());

  async function release(accountId: string) {
    if (releasingAccountIdsRef.current.has(accountId)) {
      return;
    }

    releasingAccountIdsRef.current.add(accountId);
    setNotice(null);
    setReleasingAccountId(accountId);
    try {
      await api.releasePromoCredit(accountId);
      setNotice({ kind: "success", text: "Promotional credit released." });
      setRefreshKey((value) => value + 1);
    } catch (err) {
      setNotice({ kind: "error", text: toMessage(err) });
    } finally {
      releasingAccountIdsRef.current.delete(accountId);
      setReleasingAccountId(null);
    }
  }

  return (
    <Page title="Admin">
      <Notice notice={notice} />
      <Panel title="Users">
        <InlineState state={users} label="users" />
        <table>
          <thead><tr><th>Email</th><th>Name</th><th>Role</th></tr></thead>
          <tbody>{(users.data?.users ?? []).map((user) => <tr key={user.id}><td>{user.email}</td><td>{user.firstName} {user.lastName}</td><td>{user.role}</td></tr>)}</tbody>
        </table>
      </Panel>
      <Panel title="Accounts">
        <InlineState state={accounts} label="accounts" />
        <table>
          <thead><tr><th>Account</th><th>User</th><th>Available</th><th>Pending</th><th></th></tr></thead>
          <tbody>
            {(accounts.data?.accounts ?? []).map((account) => (
              <tr key={account.id}>
                <td>{account.accountNumber}</td>
                <td>{shortId(account.userId)}</td>
                <td>{formatMoney(account.availableBalance)}</td>
                <td>{formatMoney(account.pendingBalance)}</td>
                <td>
                  <button type="button" onClick={() => release(account.id)} disabled={releasingAccountId === account.id}>
                    {releasingAccountId === account.id ? "Releasing..." : "Release promo"}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Panel>
      <div className="grid two">
        <Panel title="Payments">
          <InlineState state={payments} label="payments" />
          <PaymentTable payments={payments.data?.payments ?? []} />
        </Panel>
        <Panel title="Transfers">
          <InlineState state={transfers} label="transfers" />
          <TransferTable transfers={transfers.data?.transfers ?? []} />
        </Panel>
      </div>
      <Panel title="Audit logs">
        <InlineState state={audit} label="audit logs" />
        <table>
          <thead><tr><th>Date</th><th>Action</th><th>Entity</th></tr></thead>
          <tbody>{(audit.data?.auditLogs ?? []).map((log) => <tr key={log.id}><td>{formatDate(log.createdAt)}</td><td>{log.action}</td><td>{log.entityType} {shortId(log.entityId)}</td></tr>)}</tbody>
        </table>
      </Panel>
    </Page>
  );
}

function AccountTable({ accounts, onExport }: { accounts: Account[]; onExport: (accountId: string) => void }) {
  accounts = Array.isArray(accounts) ? accounts : [];

  if (accounts.length === 0) {
    return <EmptyState title="No accounts yet" detail="Create an account to start testing balances, deposits, and transfers." />;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Account</th>
          <th>Available</th>
          <th>Pending</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.id}>
            <td>{account.accountNumber}</td>
            <td>{formatMoney(account.availableBalance)}</td>
            <td>{formatMoney(account.pendingBalance)}</td>
            <td>{account.isActive ? "Active" : "Inactive"}</td>
            <td><button type="button" onClick={() => onExport(account.id)}>CSV</button></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function PaymentTable({ payments }: { payments: Payment[] }) {
  payments = Array.isArray(payments) ? payments : [];

  if (payments.length === 0) {
    return <EmptyState title="No payments yet" detail="Create a PaymentIntent to test the Stripe payment flow." />;
  }

  return (
    <table>
      <thead><tr><th>Date</th><th>Amount</th><th>Status</th></tr></thead>
      <tbody>{payments.map((payment) => <tr key={payment.id}><td>{formatDate(payment.createdAt)}</td><td>{formatMoney(payment.amount)} {payment.currency}</td><td>{payment.status}</td></tr>)}</tbody>
    </table>
  );
}

function TransferTable({ transfers }: { transfers: Transfer[] }) {
  transfers = Array.isArray(transfers) ? transfers : [];

  if (transfers.length === 0) {
    return <EmptyState title="No transfers yet" detail="Use the transfer form after you have at least two accounts." />;
  }

  return (
    <table>
      <thead><tr><th>Date</th><th>Amount</th><th>Status</th></tr></thead>
      <tbody>{transfers.map((transfer) => <tr key={transfer.id}><td>{formatDate(transfer.createdAt)}</td><td>{formatMoney(transfer.amount)}</td><td>{transfer.status}</td></tr>)}</tbody>
    </table>
  );
}

function Page<T>({ title, state, children }: { title: string; state?: LoadState<T>; children: ReactNode }) {
  return (
    <section>
      <header className="page-header">
        <div>
          <span className="eyebrow">BrandonFintech</span>
          <h1>{title}</h1>
        </div>
      </header>
      {state?.loading && <LoadingBlock label={`Loading ${title.toLowerCase()}...`} />}
      {state?.error && <ErrorBlock message={state.error} />}
      {children}
    </section>
  );
}

type ErrorBoundaryProps = {
  children: ReactNode;
};

type ErrorBoundaryState = {
  error: Error | null;
};

class AppErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = {
    error: null
  };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Frontend render failure", error, errorInfo);
  }

  render() {
    if (this.state.error) {
      return (
        <main className="main">
          <ErrorBlock message={this.state.error.message} />
          <button type="button" onClick={() => this.setState({ error: null })}>Try again</button>
        </main>
      );
    }

    return this.props.children;
  }
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function Metric({ label, value, tone = "slate" }: { label: string; value: ReactNode; tone?: string }) {
  return (
    <div className={`metric metric-${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function VisualPanel({ title, caption, variant }: { title: string; caption: string; variant: "trend" | "bars" | "activity" }) {
  return (
    <section className="visual-panel">
      <div>
        <h2>{title}</h2>
        <p>{caption}</p>
      </div>
      <div className={`visual visual-${variant}`} aria-hidden="true">
        <span />
        <span />
        <span />
        <span />
      </div>
    </section>
  );
}

function InlineState<T>({ state, label }: { state: LoadState<T>; label: string }) {
  if (state.loading) {
    return <p className="muted">Loading {label}...</p>;
  }

  if (state.error) {
    return <Notice notice={{ kind: "error", text: `Could not load ${label}: ${state.error}` }} />;
  }

  return null;
}

type NoticeState = {
  kind: "success" | "error";
  text: string;
} | null;

function Notice({ notice }: { notice: NoticeState }) {
  if (!notice) {
    return null;
  }

  return <p className={notice.kind === "error" ? "notice error-notice" : "notice success-notice"}>{notice.text}</p>;
}

function LoadingBlock({ label }: { label: string }) {
  return (
    <div className="loading-block skeleton-wrap" role="status" aria-live="polite">
      <span>{label}</span>
      <div className="skeleton-grid">
        <span />
        <span />
        <span />
      </div>
    </div>
  );
}

function ErrorBlock({ message }: { message: string }) {
  return (
    <div className="error-block" role="alert">
      <strong>Request failed</strong>
      <span>{message}</span>
    </div>
  );
}

function EmptyState({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="empty-state">
      <strong>{title}</strong>
      <span>{detail}</span>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  step
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  step?: string;
}) {
  return (
    <label>
      <span>{label}</span>
      <input value={value} onChange={(event) => onChange(event.target.value)} type={type} step={step} />
    </label>
  );
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<{ value: string; label: string }> }) {
  return (
    <label>
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Select</option>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}

function useLoader<T>(load: () => Promise<{ success: boolean } & T>, dependencies: unknown[]): LoadState<T> {
  const [state, setState] = useState<LoadState<T>>({ data: null, loading: true, error: null });
  const dependencyKey = useMemo(() => JSON.stringify(dependencies), dependencies);

  useEffect(() => {
    let active = true;
    setState((current) => ({ ...current, loading: true, error: null }));

    load()
      .then((data) => {
        if (active) {
          setState({ data, loading: false, error: null });
        }
      })
      .catch((err) => {
        if (active) {
          setState({ data: null, loading: false, error: toMessage(err) });
        }
      });

    return () => {
      active = false;
    };
  }, [dependencyKey]);

  return state;
}

function accountOption(account: Account) {
  return {
    value: account.id,
    label: `${account.accountNumber} (${formatMoney(account.availableBalance)})`
  };
}

function formatDate(value: string) {
  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? "Unknown" : date.toLocaleString();
}

function formatMoney(value: unknown) {
  return money.format(typeof value === "number" && Number.isFinite(value) ? value : 0);
}

function shortId(value: string | null | undefined) {
  if (!value) {
    return "";
  }

  return value.length > 8 ? value.slice(0, 8) : value;
}

function toMessage(err: unknown) {
  return err instanceof Error ? err.message : "Something went wrong";
}

export default App;
