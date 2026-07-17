# Bullet Foundry Community Server

This small Node.js service stores community posts and optional playable level JSON attachments. It has no external npm dependencies.

## API

- `GET /health`
- `GET /api/posts`
- `GET /api/posts/:id`
- `POST /api/uploads` with raw PNG, JPG, or WEBP bytes. Returns `{ "mediaUrl": "/uploads/..." }`.
- `POST /api/posts` with `{ "type": "image|level", "title": "...", "author": "...", "body": "...", "mediaUrl": "https://...", "level": "{...}" }`

Community posts are image-and-text posts. The game uploads a selected PNG, JPG, or WEBP image directly to the server before it creates the post; uploads are limited to 6 MiB and are rendered in grayscale in the game. Playable levels can still be published from the Level Editor. Level JSON attachments are limited to 128 KiB, at most 1,000 spawns, and the enemy ids currently used by the game.

## Local Run

```bash
cd Tools/community-level-server
PORT=8787 node server.js
curl http://127.0.0.1:8787/health
```

The server stores its runtime data in `data/`. Back up that directory before moving or replacing a server.

## Nest Deployment

This project is configured for a root-managed Debian Nest container. It keeps the Node API private on `127.0.0.1:8787` and Caddy exposes HTTPS on ports 80 and 443.

1. Install the runtime:

```bash
apt-get update
apt-get install -y nodejs caddy
```

2. Upload this directory to `/opt/community-level-server`.
3. Choose a hostname with an AAAA record pointing to the Nest public IPv6 address. The included `Caddyfile` currently uses `2a01-4f9-3081-399c--1306.nip.io` for this server. Replace that hostname with a custom domain when one is available.
4. Install and start the API service:

```bash
install -m 0644 bullet-foundry-community.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now bullet-foundry-community
```

5. Install the proxy and reload it:

```bash
install -m 0644 Caddyfile /etc/caddy/Caddyfile
caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
systemctl enable --now caddy
systemctl reload caddy
```

6. Verify `https://YOUR_HOSTNAME/health` returns `{ "ok": true }`.
7. In Unity, set both `Community API Base Url` fields to `https://YOUR_HOSTNAME`:
   - `Level Editor Controller` for publishing.
   - `Community Button` in the `LevelSelect` scene for browsing.

The browser build requires HTTPS. Do not point it to `http://` or to the private port `8787`.
