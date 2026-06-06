# BrandonFintech Deployment Steps

This guide prepares the current MVP for deployment without changing business logic. Keep all real secrets in hosting-provider secret stores.

## Deployment Targets

- Frontend: Cloudflare Pages
- Worker: Cloudflare Workers
- API: Render, Railway, Fly.io, or Azure
- Database: Managed PostgreSQL

## Production Environment Checklist

### API

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Postgres=Host=YOUR_POSTGRES_HOST;Port=5432;Database=brandonfintech;Username=brandonfintech_app;Password=REPLACE_WITH_SECRET;SSL Mode=Require;Trust Server Certificate=false
Jwt__Issuer=https://api.YOURDOMAIN.com
Jwt__Audience=BrandonFintechUsers
Jwt__Secret=REPLACE_WITH_LONG_RANDOM_SECRET
Stripe__SecretKey=sk_test_REPLACE_ME
Stripe__WebhookSecret=whsec_REPLACE_ME
```

### Frontend

```text
VITE_API_BASE_URL=https://api.YOURDOMAIN.com
```

### Cloudflare Worker

```text
OLLAMA_BASE_URL=https://ollama.YOURDOMAIN.com
OLLAMA_MODEL=tinyllama
```

### CORS

Production API CORS should allow only trusted frontend origins:

```text
https://app.YOURDOMAIN.com
https://YOUR-PAGES-PROJECT.pages.dev
```

Do not deploy to real users with wildcard CORS.

## API Docker Build

Build from the repository root and point Docker at the API Dockerfile:

```powershell
docker build -f BrandonFintech.Api/Dockerfile -t brandonfintech-api:latest .
```

Run locally with environment variables:

```powershell
docker run --rm -p 8080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e "ConnectionStrings__Postgres=Host=host.docker.internal;Port=5432;Database=credit_eoscar_flow;Username=credit_app;Password=credit_app_password" `
  -e "Jwt__Issuer=BrandonFintech" `
  -e "Jwt__Audience=BrandonFintechUsers" `
  -e "Jwt__Secret=REPLACE_WITH_LONG_RANDOM_SECRET" `
  -e "Stripe__SecretKey=sk_test_REPLACE_ME" `
  -e "Stripe__WebhookSecret=whsec_REPLACE_ME" `
  brandonfintech-api:latest
```

## Deployment Order

1. Provision managed PostgreSQL.
2. Create the production database and least-privilege app user.
3. Configure API environment variables and secrets.
4. Deploy API to Render, Railway, Fly.io, or Azure.
5. Run EF Core migrations against the production database.
6. Verify API `/health` and `/ready`.
7. Configure Stripe webhook endpoint:

```text
https://api.YOURDOMAIN.com/api/v1/payments/stripe/webhook
```

8. Configure Stripe webhook events:

```text
payment_intent.succeeded
payment_intent.payment_failed
payment_intent.canceled
```

9. Deploy frontend to Cloudflare Pages with `VITE_API_BASE_URL`.
10. Deploy Cloudflare Worker with `OLLAMA_BASE_URL` and `OLLAMA_MODEL`.
11. Configure domain DNS for frontend, API, Worker, and Ollama tunnel.
12. Restrict API CORS to the final frontend domains.

## Migration Command

Run from a machine with production database access:

```powershell
dotnet ef database update --project BrandonFintech.Infrastructure --startup-project BrandonFintech.Api
```

## Post-Deployment Smoke Tests

- Register a new user.
- Login and receive JWT.
- Load dashboard.
- Confirm signup account has `AvailableBalance = 0` and `PendingBalance = 500`.
- Deposit into an owned account.
- Create an internal transfer.
- Export account statement CSV.
- Create Stripe PaymentIntent in test mode.
- Trigger Stripe webhook and confirm local payment status updates.
- Promote or seed an admin user, then access admin endpoints.
- Release promotional credit as admin.
- Confirm Cloudflare Worker `/health`.
- Confirm Worker `/ai/chat` if Ollama tunnel is configured.

## Remaining Blockers Before Production

- Replace wildcard CORS with an environment-driven allowlist.
- Remove placeholder values from `appsettings.json` for real deployments and rely on platform secrets.
- Add admin MFA.
- Add login/register rate limiting and lockout.
- Add refresh token rotation or token revocation.
- Add Stripe webhook event persistence and replay protection.
- Upgrade ledger to a stricter immutable double-entry design.
- Add structured audit actor metadata.
- Add automated integration tests for deployment smoke paths.
- Protect Ollama tunnel and Worker AI route with authentication/rate limits.
