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

## Acceso

La app esta protegida con Cloudflare Access en Zero Trust:

- Produccion: `pronostico-abasto.pages.dev`
- Previews/deploys directos: `*.pronostico-abasto.pages.dev`
- Login: One-time PIN por correo
- Politica: `Pronostico Abasto - Allow intelix.biz`
- Permitidos: correos con dominio `@intelix.biz`

Una persona sin sesion valida debe recibir redireccion `302` hacia
`late-brook-fd0b.cloudflareaccess.com` antes de ver la app o cualquier ruta
`/api/...`.

## Archivos cargados

Las cargas procesadas de Excel se guardan con historial y restauracion:

- Si existe un binding R2 llamado `FILES`, el JSON procesado se guarda en R2.
- Si R2 no esta habilitado, la app mantiene compatibilidad y guarda por chunks en D1.
- D1 conserva los metadatos, la carga activa y el historial de versiones.

Para activar R2 en produccion:

1. Habilitar R2 desde Cloudflare Dashboard para la cuenta.
2. Crear el bucket `pronostico-abasto-files`.
3. Agregar el binding en `wrangler.jsonc`:

```jsonc
"r2_buckets": [
  {
    "binding": "FILES",
    "bucket_name": "pronostico-abasto-files",
    "preview_bucket_name": "pronostico-abasto-files"
  }
]
```

4. Desplegar nuevamente.

Mientras R2 no este habilitado, no activar ese bloque en `wrangler.jsonc`; el deploy seguira usando D1.

## Respaldos

Exportar D1 remoto:

```powershell
npm run backup:remote
```

Desde la UI tambien se puede descargar un respaldo JSON por empresa con los datos operativos y metadatos de cargas.

Para Pages con GitHub, configurar el proyecto con root `web`, build command `npm ci && npm run build` y output directory `dist`.
