# BrandonFintech.Web

React + Vite frontend for the BrandonFintech MVP.

## Setup

```powershell
cd C:\Users\nukda\Projects\BrandonFintech\BrandonFintech.Web
npm install
Copy-Item .env.example .env
npm run dev
```

The default API base URL is:

```text
http://localhost:5015
```

Override it in `.env`:

```text
VITE_API_BASE_URL=http://localhost:5015
```

## Backend

Start the API separately:

```powershell
cd C:\Users\nukda\Projects\BrandonFintech
& 'C:\Program Files\dotnet\dotnet.exe' run --project BrandonFintech.Api\BrandonFintech.Api.csproj --launch-profile http
```

## Build

```powershell
npm run build
```

## Production Environment

Create a production environment variable in your hosting provider:

```text
VITE_API_BASE_URL=https://api.YOURDOMAIN.com
```

The same value is shown in `.env.production.example`.

## Cloudflare Pages

Use these settings:

```text
Framework preset: Vite
Root directory: BrandonFintech.Web
Build command: npm run build
Build output directory: dist
```

Set `VITE_API_BASE_URL` in Cloudflare Pages environment variables. The API domain must allow the Pages origin through CORS.

## Vercel

Use these settings:

```text
Framework preset: Vite
Root directory: BrandonFintech.Web
Build command: npm run build
Output directory: dist
```

Set `VITE_API_BASE_URL` in Vercel Project Settings under Environment Variables. The API domain must allow the Vercel deployment origin through CORS.
