# Production deployment

This document describes the containerized production architecture, configuration, security model, VDS bootstrap, and GitHub Actions CI/CD workflows for Pinveria. Paths are relative to the repository root.

## Architecture and routing

Production runs on a single VDS through [compose.yml](../compose.yml) on a fixed bridge subnet (`172.28.0.0/24`), exposing only ports `80` and `443` on the reverse proxy:

- **`caddy`** ([Caddyfile](../infra/caddy/Caddyfile) built via [frontend.Dockerfile](../infra/docker/frontend.Dockerfile)): terminates TLS (Let's Encrypt), redirects `www.<DOMAIN>` to `<DOMAIN>`, applies HTTP security headers, and routes all traffic on a single HTTPS origin:
  - `/api/*` → strips `/api` and forwards REST and SignalR (`/api/hubs/map`) to `api:8080`.
  - `/map-tiles/*` and `/media/files/*` → forwards unchanged to `api:8080`.
  - `/admin` → permanent redirect to `/admin/`.
  - `/admin/*` → serves the compiled admin SPA from `/srv/admin` with `index.html` fallback for application routes and real `404` responses for missing static assets.
  - `/` (all other paths, including `/robots.txt`, `/sitemap.xml`, `/og/*`) → forwards to the Hono SSR server at `web:4000`.
- **`web`** ([frontend.Dockerfile](../infra/docker/frontend.Dockerfile)): runs the Node.js 22 Hono SSR application as non-root `node` with `PINVERIA_API_URL=http://api:8080`, `PUBLIC_SITE_URL=https://${DOMAIN}`, and `NG_ALLOWED_HOSTS=${DOMAIN},localhost,127.0.0.1`. Exposes `/healthz`.
- **`api`** ([api.Dockerfile](../infra/docker/api.Dockerfile)): runs `Pinveria.Api` on Debian (`mcr.microsoft.com/dotnet/aspnet:11.0`) as non-root `app` with `libmediainfo0v5` installed so native `NetVips` and `MediaInfo` libraries work out of the box. Trusts forwarded headers from `172.28.0.0/24` through [`HostingOptions`](../src/Pinveria.Api/Hosting/HostingOptions.cs), disables internal HTTPS redirection behind Caddy (`Hosting__UseHttpsRedirection=false`), and persists map tiles, media files, and Data Protection keys on dedicated volumes. Exposes `/healthz`.
- **`migrator`**: runs the self-contained EF Core migration bundle (`/app/efbundle`) to completion against `postgres` before `api` starts.
- **`postgres`**: runs PostgreSQL 17 on the internal Docker network without publishing port `5432` to the host.

## Security model for a public repository

Because the repository is public:

- No server IP addresses, SSH ports, usernames, domains, or secrets are committed to tracked files. [`.env.example`](../.env.example) and [`inventory.example.yml`](../infra/ansible/inventory.example.yml) contain only safe placeholders and environment variable lookups.
- Production secrets and environment variables live exclusively in the GitHub **`production` Environment** (`Settings → Environments → production`).
- Docker images are built on GitHub-hosted runners and streamed directly to the VDS over SSH (`docker save | zstd | ssh ... docker load`) without publishing images to a public registry.
- `/opt/pinveria/.env` is generated remotely during deployment with `umask 077` (`0600` permissions). One-time administrator bootstrap credentials (`ADMIN_BOOTSTRAP_LOGIN` / `ADMIN_BOOTSTRAP_PASSWORD`) are cleared from `/opt/pinveria/.env` immediately after the initial deployment starts.

## GitHub Environment configuration

Create a GitHub Environment named **`production`** (`Settings → Environments → New environment`) and configure the following values:

### Environment Variables (`vars`)

| Variable        | Required / Default     | Purpose                                                     |
| --------------- | ---------------------- | ----------------------------------------------------------- |
| `DOMAIN`        | **Required** (`example.com`) | Primary public domain (without `https://`, no fallback) |
| `POSTGRES_DB`   | Optional (`pinveria`)  | PostgreSQL database name                                    |
| `POSTGRES_USER` | Optional (`pinveria`)  | PostgreSQL user name                                        |

### Environment Secrets (`secrets`)

| Secret                     | Required            | Purpose                                                                                      |
| -------------------------- | ------------------- | -------------------------------------------------------------------------------------------- |
| `DEPLOY_HOST`              | Yes                 | VDS public IPv4 address (or hostname)                                                        |
| `DEPLOY_PORT`              | Optional (`22`)     | SSH port on the VDS                                                                          |
| `DEPLOY_USER`              | Optional (`deploy`) | Non-root deployment user on the VDS                                                          |
| `DEPLOY_SSH_KEY`           | Yes                 | Private Ed25519 SSH key for `DEPLOY_USER`                                                    |
| `DEPLOY_SSH_PUBLIC_KEY`    | Bootstrap only      | Public Ed25519 SSH key installed for `DEPLOY_USER` by the Ansible bootstrap playbook         |
| `BOOTSTRAP_USER`           | Bootstrap only      | Initial SSH user provided by the VDS host (defaults to `root`)                               |
| `BOOTSTRAP_SSH_KEY`        | Bootstrap only      | Optional initial private SSH key if different from `DEPLOY_SSH_KEY`                          |
| `DEPLOY_KNOWN_HOSTS`       | **Required for deploy** | Output of `ssh-keyscan -H <DEPLOY_HOST>` to pin the server host key against MITM         |
| `POSTGRES_PASSWORD`        | Yes                 | Strong random password for PostgreSQL                                                        |
| `ADMIN_BOOTSTRAP_LOGIN`    | First deploy only   | Initial administrator login (used only when `bootstrap_admin` is checked)                    |
| `ADMIN_BOOTSTRAP_PASSWORD` | First deploy only   | Initial administrator password (remove secret after the first administrator is provisioned)  |

## Step-by-step launch checklist

1. **Configure DNS**: create `A` records for `@` (`example.com`) and `www` (`www.example.com`) pointing directly to the VDS public IPv4 address.
2. **Configure GitHub Environment**: create the `production` environment in GitHub and fill in the variables and secrets above (`DEPLOY_KNOWN_HOSTS` can be obtained with `ssh-keyscan -p <port> -H <vds-ip>` once the VDS is provisioned).
3. **Bootstrap the VDS**: run the [**Bootstrap VDS**](../.github/workflows/bootstrap-vds.yml) workflow (`Actions → Bootstrap VDS → Run workflow`), which executes [`bootstrap.yml`](../infra/ansible/playbooks/bootstrap.yml) to create the `deploy` user, disable root and password SSH login, configure `ufw` (`22`, `80`, `443`), `fail2ban`, `2 GB` swap, and Docker Engine with log rotation.
4. **First deployment (with administrator provisioning)**: run the [**Deploy**](../.github/workflows/deploy.yml) workflow (`Actions → Deploy → Run workflow`) with **`bootstrap_admin: true`**. Once verified, delete `ADMIN_BOOTSTRAP_LOGIN` and `ADMIN_BOOTSTRAP_PASSWORD` from GitHub Secrets.
5. **Subsequent deployments**: push a version tag (`v*`, such as `git tag v1.0.0 && git push origin v1.0.0`) or trigger **Deploy** manually with `bootstrap_admin: false`.
