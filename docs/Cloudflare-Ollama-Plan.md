# Cloudflare Worker + Ollama Integration Plan

## Purpose

BrandonFintech should keep the fintech API isolated from direct model runtime concerns. A Cloudflare Worker can act as a small AI gateway that calls an Ollama endpoint and later exposes a stable API surface to BrandonFintech.

## Localhost Limitation

Cloudflare Workers run on Cloudflare's edge network, not on the developer workstation. A deployed Worker cannot call `localhost`, `127.0.0.1`, or a private Docker hostname on the local machine. Those addresses resolve inside the Worker runtime environment, not inside the local development network.

## Safe Ollama Exposure

Use Cloudflare Tunnel to expose the local Ollama service through a controlled HTTPS hostname. The tunnel should point to the local Ollama service, commonly `http://localhost:11434`, and publish a Cloudflare-managed URL or a hostname under a domain you control.

The Worker should call only the tunnel URL configured through an environment variable:

```text
OLLAMA_BASE_URL=https://ollama.example.com
```

Do not hardcode the tunnel URL in source code if it differs by environment.

## Worker Flow

1. Client calls `POST /ai/chat` on the Worker with `{ "message": "..." }`.
2. Worker validates the JSON body and message.
3. Worker reads `OLLAMA_BASE_URL` from environment.
4. Worker reads `OLLAMA_MODEL` from environment, defaulting to `llama3.1`.
5. Worker calls:

```text
POST {OLLAMA_BASE_URL}/api/generate
```

with:

```json
{
  "model": "llama3.1",
  "prompt": "user message",
  "stream": false
}
```

6. Worker returns the model response in a safe JSON envelope.

## BrandonFintech Integration Options

There are two safe future directions:

1. BrandonFintech API calls the Worker.
   - Best when BrandonFintech owns auth, audit logs, rate limits, and user context.
   - Worker stays a pure model gateway.

2. Worker calls BrandonFintech API.
   - Best for edge-first user interfaces.
   - Requires service authentication, request signing, rate limits, and strict authorization checks before touching fintech data.

For the current scaffold, the Worker does not call BrandonFintech and BrandonFintech does not call the Worker. This keeps the backend stable while the AI gateway is developed independently.

## Security Notes

- Store `OLLAMA_BASE_URL` and optional `OLLAMA_MODEL` as Cloudflare Worker environment variables or secrets.
- Do not expose Ollama broadly without Cloudflare Access, firewall rules, or another access control layer.
- Do not pass bank credentials, JWTs, account numbers, payment identifiers, or PII to Ollama in this phase.
- Add request authentication, rate limits, logging, and abuse protection before production use.
- Treat model output as untrusted text.
