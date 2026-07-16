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

1. SSH into Nest and run `nest get_port`. Keep the allocated port number.
2. Clone or upload this repository, then enter `Tools/community-level-server`.
3. Confirm Node 18 or newer with `node --version`.
4. Start a quick test with `PORT=YOUR_PORT node server.js`, then from another terminal run `curl http://hackclub.app:YOUR_PORT/health`.
5. Copy `bullet-foundry-community.service` to `~/.config/systemd/user/`, replace the two `REPLACE_...` values, then run:

```bash
systemctl --user daemon-reload
systemctl --user enable --now bullet-foundry-community
systemctl --user status bullet-foundry-community
```

6. In Unity, set both `Community API Base Url` fields to `http://hackclub.app:YOUR_PORT`:
   - `Level Editor Controller` for publishing.
   - `Community Button` in the `LevelSelect` scene for browsing.

For an HTTPS-only WebGL build, put the API behind an HTTPS reverse proxy or custom domain; an HTTPS page cannot call this plain HTTP address because browsers block mixed content.
