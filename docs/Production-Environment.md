# BrandonFintech Production Environment

Production configuration must come from hosting-provider environment variables or secret stores. Do not commit real secrets, database passwords, Stripe keys, webhook secrets, tunnel URLs, or JWT signing secrets.

## API Environment Variables

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Postgres=Host=YOUR_POSTGRES_HOST;Port=5432;Database=brandonfintech;Username=brandonfintech_app;Password=REPLACE_WITH_SECRET;SSL Mode=Require;Trust Server Certificate=false
Jwt__Issuer=https://api.YOURDOMAIN.com
Jwt__Audience=BrandonFintechUsers
Jwt__Secret=REPLACE_WITH_LONG_RANDOM_SECRET
Stripe__SecretKey=sk_test_REPLACE_ME
Stripe__WebhookSecret=whsec_REPLACE_ME
Cors__AllowedOrigins=https://fintech.YOURDOMAIN.com
```

Use `sk_test_...` and test webhook secrets until the full live-mode checklist is complete. Use `sk_live_...` only after the live webhook endpoint, CORS, admin controls, logging, and database backups are verified.

## Frontend Environment Variables

```text
VITE_API_BASE_URL=https://api.YOURDOMAIN.com
```

Set this in Cloudflare Pages or Vercel environment variables. The value is baked into the built static assets.

## Worker Environment Variables

```text
OLLAMA_BASE_URL=https://ollama.YOURDOMAIN.com
OLLAMA_MODEL=tinyllama
```

For a deployed Cloudflare Worker, `OLLAMA_BASE_URL` must be an HTTPS URL reachable from Cloudflare, normally a Cloudflare Tunnel hostname. Do not use `localhost` in production.

## Stripe Webhook Configuration

Create a Stripe webhook endpoint:

```text
https://api.YOURDOMAIN.com/api/v1/payments/stripe/webhook
```

Subscribe to:

```text
payment_intent.succeeded
payment_intent.payment_failed
payment_intent.canceled
```

Copy the signing secret into `Stripe__WebhookSecret`.

## PostgreSQL Connection String

Use managed PostgreSQL with SSL required, automated backups, and a dedicated least-privilege app user. Apply migrations before sending traffic to the API:

```powershell
dotnet ef database update --project BrandonFintech.Infrastructure --startup-project BrandonFintech.Api
```

## CORS Domain Allowlist

Production CORS should allow only trusted frontend origins, for example:

```text
https://app.YOURDOMAIN.com
https://YOUR-PAGES-PROJECT.pages.dev
```

Do not leave wildcard CORS enabled for real users.

## Secret Handling Rules

- Keep real values out of `appsettings.json`, `.env`, `wrangler.jsonc`, and source control.
- Use platform secret managers: Render/Railway/Fly.io/Azure app settings, Cloudflare Pages env vars, Cloudflare Worker secrets, or equivalent.
- Rotate `Jwt__Secret`, Stripe keys, and database credentials before any public launch if they were ever exposed locally.
- Never log Stripe secrets, JWT signing secrets, database passwords, or webhook secrets.
