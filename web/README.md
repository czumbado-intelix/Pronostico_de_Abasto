# Pronostico de Abasto Web

Version web del flujo de pronostico de abasto para Cloudflare Pages.

## Desarrollo local

```powershell
npm ci
npm run dev
```

## Verificacion

```powershell
npm run build
npm test
npm run lint
npx wrangler pages functions build functions --outdir .wrangler\functions-build --project-directory .
```

## Cloudflare

El proyecto usa Pages Functions y D1. La base configurada en `wrangler.jsonc` es:

- Binding: `DB`
- Database: `pronostico-abasto`
- Database ID: `a0500cae-a137-4fc2-99c2-f6964df20b8f`

Aplicar migraciones:

```powershell
npm run cf:migrate:local
npm run cf:migrate:remote
```

Para Pages con GitHub, configurar el proyecto con root `web`, build command `npm ci && npm run build` y output directory `dist`.
