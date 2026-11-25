# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS build
WORKDIR /workspace

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm exec nx -- build web --configuration=production \
	&& npm exec nx -- build admin --configuration=production

FROM node:22-bookworm-slim AS runtime-deps
WORKDIR /runtime

COPY frontend/package.json ./
RUN node -e "\
const fs = require('node:fs'); \
const rootPkg = JSON.parse(fs.readFileSync('package.json', 'utf8')); \
const minPkg = { \
  name: 'pinveria-web-runtime', \
  private: true, \
  type: 'module', \
  dependencies: { \
    '@fontsource/inter': rootPkg.dependencies['@fontsource/inter'], \
    '@resvg/resvg-js': rootPkg.dependencies['@resvg/resvg-js'], \
    'sharp': rootPkg.dependencies['sharp'] \
  } \
}; \
fs.writeFileSync('package.json', JSON.stringify(minPkg, null, 2)); \
" && npm install --omit=dev --no-audit --no-fund

FROM node:22-bookworm-slim AS web
WORKDIR /app

ENV NODE_ENV=production \
	PORT=4000

COPY --from=runtime-deps /runtime/node_modules ./node_modules
COPY --from=build /workspace/dist/apps/web ./dist/apps/web

EXPOSE 4000
USER node

CMD ["node", "dist/apps/web/server/server.mjs"]

FROM caddy:2.10-alpine AS caddy
COPY infra/caddy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /workspace/dist/apps/admin/browser /srv/admin
