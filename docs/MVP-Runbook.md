# BrandonFintech MVP Runbook

This runbook covers local MVP startup and smoke testing for the BrandonFintech API, React frontend, Stripe test payments, and Cloudflare Worker Ollama gateway.

## 1. Start PostgreSQL In Docker

```powershell
docker run --name brandonfintech-postgres `
  -e POSTGRES_DB=credit_eoscar_flow `
  -e POSTGRES_USER=credit_app `
  -e POSTGRES_PASSWORD=credit_app_password `
  -p 5432:5432 `
  -d postgres:16-alpine
```

If the container already exists:

```powershell
docker start brandonfintech-postgres
```

## 2. Start BrandonFintech API

```powershell
cd C:\Users\nukda\Projects\BrandonFintech
& "C:\Program Files\dotnet\dotnet.exe" run --project BrandonFintech.Api\BrandonFintech.Api.csproj --launch-profile http
```

Default local API URL:

```text
http://localhost:5015
```

Health checks:

```powershell
Invoke-RestMethod http://localhost:5015/health
Invoke-RestMethod http://localhost:5015/ready
```

## 3. Start BrandonFintech Web

```powershell
cd C:\Users\nukda\Projects\BrandonFintech\BrandonFintech.Web
npm install
Copy-Item .env.example .env
npm run dev
```

Default frontend URL:

```text
http://localhost:5173
```

## 4. Test Auth, Register, Login

The API now uses ASP.NET Core `PasswordHasher<User>`. Legacy SHA256 hashes are upgraded on successful login.

Register creates:

- User with `Role = "User"`
- Default account
- `AvailableBalance = 0`
- `PendingBalance = 500.00`
- Ledger entry `PromotionalCreditPending`
- Audit log `PromotionalCreditPendingCreated`

```powershell
$baseUrl = "http://localhost:5015"
$email = "mvp.$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())@example.com"
$password = "Test12345!"

$registerBody = @{
  email = $email
  password = $password
  firstName = "MVP"
  lastName = "User"
} | ConvertTo-Json

$registered = Invoke-RestMethod "$baseUrl/api/v1/auth/register" `
  -Method Post `
  -Body $registerBody `
  -ContentType "application/json"

$accountId = $registered.defaultAccount.id
```

Login and store JWT:

```powershell
$loginBody = @{ email = $email; password = $password } | ConvertTo-Json
$login = Invoke-RestMethod "$baseUrl/api/v1/auth/login" `
  -Method Post `
  -Body $loginBody `
  -ContentType "application/json"

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }
```

Verify current user:

```powershell
Invoke-RestMethod "$baseUrl/api/v1/auth/me" -Headers $headers
```

## 5. Test Accounts And Deposits

Money-moving POST endpoints require `Idempotency-Key`.

Create an extra account:

```powershell
$accountHeaders = $headers.Clone()
$accountHeaders["Idempotency-Key"] = [guid]::NewGuid().ToString()

$createdAccount = Invoke-RestMethod "$baseUrl/api/v1/accounts" `
  -Method Post `
  -Headers $accountHeaders `
  -Body "{}" `
  -ContentType "application/json"
```

Deposit into an owned account:

```powershell
$depositHeaders = $headers.Clone()
$depositHeaders["Idempotency-Key"] = [guid]::NewGuid().ToString()

$depositBody = @{
  amount = 25.00
  description = "MVP deposit"
} | ConvertTo-Json

Invoke-RestMethod "$baseUrl/api/v1/accounts/$accountId/deposit" `
  -Method Post `
  -Headers $depositHeaders `
  -Body $depositBody `
  -ContentType "application/json"
```

List accounts and export CSV statement:

```powershell
Invoke-RestMethod "$baseUrl/api/v1/accounts" -Headers $headers
Invoke-WebRequest "$baseUrl/api/v1/accounts/$accountId/statement.csv" -Headers $headers -OutFile statement.csv
```

## 6. Test Internal Transfers

Create or select a destination account, then submit transfer with an idempotency key:

```powershell
$transferHeaders = $headers.Clone()
$transferHeaders["Idempotency-Key"] = [guid]::NewGuid().ToString()

$transferBody = @{
  fromAccountId = $accountId
  toAccountId = $createdAccount.account.id
  amount = 10.00
  description = "MVP transfer test"
} | ConvertTo-Json

$transfer = Invoke-RestMethod "$baseUrl/api/v1/transfers/internal" `
  -Method Post `
  -Headers $transferHeaders `
  -Body $transferBody `
  -ContentType "application/json"
```

## 7. Test Stripe PaymentIntent

Set Stripe test secrets through .NET User Secrets or environment variables, not source files:

```powershell
cd C:\Users\nukda\Projects\BrandonFintech\BrandonFintech.Api
& "C:\Program Files\dotnet\dotnet.exe" user-secrets set "Stripe:SecretKey" "sk_test_REPLACE_ME"
```

Create PaymentIntent:

```powershell
$paymentHeaders = $headers.Clone()
$paymentHeaders["Idempotency-Key"] = [guid]::NewGuid().ToString()

$paymentBody = @{
  amount = 12.34
  currency = "USD"
} | ConvertTo-Json

$payment = Invoke-RestMethod "$baseUrl/api/v1/payments/intents" `
  -Method Post `
  -Headers $paymentHeaders `
  -Body $paymentBody `
  -ContentType "application/json"
```

## 8. Test Stripe Webhook With Stripe CLI

```powershell
cd C:\Users\nukda\Projects\BrandonFintech\BrandonFintech.Api
& "C:\Program Files\dotnet\dotnet.exe" user-secrets set "Stripe:WebhookSecret" "whsec_REPLACE_ME"
stripe listen --forward-to http://localhost:5015/api/v1/payments/stripe/webhook
stripe trigger payment_intent.succeeded
```

The webhook verifies Stripe signatures and handles:

- `payment_intent.succeeded`
- `payment_intent.payment_failed`
- `payment_intent.canceled`

## 9. Test Admin Role And Promo Credit Release

Development-only promotion endpoint:

```powershell
Invoke-RestMethod "$baseUrl/api/v1/admin/dev/promote" -Method Post -Headers $headers
```

Log in again to receive an admin JWT, then release pending promotional credit:

```powershell
$adminLogin = Invoke-RestMethod "$baseUrl/api/v1/auth/login" `
  -Method Post `
  -Body $loginBody `
  -ContentType "application/json"

$adminHeaders = @{ Authorization = "Bearer $($adminLogin.accessToken)" }

Invoke-RestMethod "$baseUrl/api/v1/admin/accounts/$accountId/release-promo-credit" `
  -Method Post `
  -Headers $adminHeaders
```

## 10. Test Dashboard And Transactions

```powershell
Invoke-RestMethod "$baseUrl/api/v1/dashboard/summary" -Headers $headers
Invoke-RestMethod "$baseUrl/api/v1/transactions" -Headers $headers
```

## 11. Start Ollama

```powershell
ollama serve
ollama pull tinyllama
```

## 12. Start Cloudflare Worker

```powershell
cd C:\Users\nukda\Projects\BrandonFintech\cloudflare-worker
npm install
npx wrangler dev
```

Local config uses:

```text
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=tinyllama
```

Test:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/ai/chat `
  -Method Post `
  -Body (@{ message = "Reply with exactly: hello" } | ConvertTo-Json) `
  -ContentType "application/json"
```

## 13. Current Limitations

- Admin MFA is not implemented.
- JWT refresh tokens and revocation are not implemented.
- Ledger entries exist but are not yet a full immutable double-entry ledger.
- Stripe fulfillment and webhook event persistence are not complete.
- Worker AI gateway has no authentication, rate limits, or BrandonFintech audit integration.
- Production CORS allowlisting still needs to replace permissive local CORS.
- Automated integration tests are not yet present.

## 14. What Is Not Production-Ready Yet

- Production secrets must move to platform secret stores.
- API deployment manifest or Dockerfile is not present.
- Managed PostgreSQL backup, SSL, and migration process must be configured.
- Admin MFA, rate limiting, lockout, and session revocation are required before real users.
- Stripe live-mode operations require a separate checklist and live webhook validation.
- Ollama must be exposed only through a protected Cloudflare Tunnel or equivalent access control.

## 15. Next Features To Build

1. Add production CORS allowlist configuration.
2. Add admin MFA.
3. Add refresh token rotation and token revocation.
4. Upgrade ledger to immutable double-entry journal entries.
5. Add Stripe webhook event persistence and replay protection.
6. Add reconciliation jobs for ledger/payment consistency.
7. Add structured audit actor metadata.
8. Add integration tests for auth, accounts, deposits, transfers, payments, webhooks, admin, and frontend flows.
9. Add API Dockerfile and deployment manifest.
