# BrandonFintech AI Gateway Worker

Cloudflare Worker scaffold for safely routing AI chat requests to an Ollama server exposed through Cloudflare Tunnel.

## Endpoints

```text
GET  /health
POST /ai/chat
```

`POST /ai/chat` accepts:

```json
{
  "message": "Explain ledger posting"
}
```

## Configuration

Example values are documented in `.env.example`.

Set the Ollama base URL as an environment variable or secret:

```powershell
npx wrangler secret put OLLAMA_BASE_URL
```

Use the Cloudflare Tunnel HTTPS URL, not `localhost`.

Optional model override:

```powershell
npx wrangler secret put OLLAMA_MODEL
```

If `OLLAMA_MODEL` is not set, the Worker defaults to `llama3.1`. Local MVP testing currently uses `tinyllama`.

## Local Development

```powershell
npm install
npx wrangler dev
```

For local testing, provide a non-secret local variable only if appropriate:

```powershell
npx wrangler dev --var OLLAMA_BASE_URL:https://your-ollama-tunnel.example.com
```

The checked-in `wrangler.jsonc` uses `http://localhost:11434` for local development only. Replace it with a Cloudflare Tunnel HTTPS URL through Cloudflare environment variables or secrets before deployment.

## Deploy

```powershell
npx wrangler deploy
```

## Notes

- Do not commit real tunnel URLs, API keys, or secrets.
- Protect the Ollama tunnel with Cloudflare Access or equivalent controls before production use.
- The Worker does not call the BrandonFintech API yet.
