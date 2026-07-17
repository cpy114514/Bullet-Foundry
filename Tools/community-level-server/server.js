"use strict";

const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const http = require("node:http");
const path = require("node:path");

const port = Number.parseInt(process.env.PORT || "8787", 10);
const corsOrigin = process.env.CORS_ORIGIN || "*";
const dataDirectory = path.join(__dirname, "data");
const uploadsDirectory = path.join(dataDirectory, "uploads");
const postsIndexPath = path.join(dataDirectory, "posts.json");
const accountsPath = path.join(dataDirectory, "accounts.json");
const sessionsPath = path.join(dataDirectory, "sessions.json");
const maxRequestBytes = 128 * 1024;
const maxImageBytes = 6 * 1024 * 1024;
const maxPostBodyLength = 4000;
const maxCommentLength = 800;
const maxSpawns = 1000;
const postTypes = new Set(["image", "level"]);
const imageContentTypes = new Map([
  ["image/png", ".png"],
  ["image/jpeg", ".jpg"],
  ["image/webp", ".webp"]
]);
const knownEnemies = new Set(["Goblin", "SpeedGoblin", "Barbarian", "PigLeader", "FrogPrincess", "Chicken", "Giant"]);

async function ensureStorage() {
  await fs.mkdir(dataDirectory, { recursive: true });
  await fs.mkdir(uploadsDirectory, { recursive: true });
  for (const file of [postsIndexPath, accountsPath, sessionsPath]) {
    try { await fs.access(file); } catch { await writeJson(file, []); }
  }
}

async function readJson(filePath, fallback = []) {
  try { const result = JSON.parse(await fs.readFile(filePath, "utf8")); return Array.isArray(result) ? result : fallback; } catch { return fallback; }
}

const readPosts = () => readJson(postsIndexPath);
const readAccounts = () => readJson(accountsPath);
const readSessions = () => readJson(sessionsPath);

async function writeJson(filePath, value) {
  const temporaryPath = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  await fs.writeFile(temporaryPath, JSON.stringify(value, null, 2), "utf8");
  await fs.rename(temporaryPath, filePath);
}

function send(response, status, body) {
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": corsOrigin,
    "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Authorization"
  });
  response.end(JSON.stringify(body));
}

function cleanText(value, fallback, maximumLength) {
  const cleaned = typeof value === "string" ? value.trim().replace(/\s+/g, " ") : "";
  return (cleaned || fallback).slice(0, maximumLength);
}
function cleanBody(value) { return typeof value === "string" ? value.trim().slice(0, maxPostBodyLength) : ""; }
function cleanComment(value) { return typeof value === "string" ? value.trim().slice(0, maxCommentLength) : ""; }
function cleanMediaUrl(value) {
  if (typeof value !== "string" || value.length > 2048) return "";
  try { const url = new URL(value.trim()); return url.protocol === "http:" || url.protocol === "https:" ? url.href : ""; } catch { return ""; }
}
function usernameError(username) {
  return /^[A-Za-z0-9_-]{3,24}$/.test(username || "") ? "" : "Username must be 3-24 letters, numbers, _ or -.";
}
function hashPassword(password, salt) { return crypto.scryptSync(password, salt, 64).toString("hex"); }
function safeEqual(left, right) {
  const a = Buffer.from(left, "hex"); const b = Buffer.from(right, "hex");
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}
function bearerToken(request) {
  const header = String(request.headers.authorization || "");
  return header.startsWith("Bearer ") ? header.slice(7).trim() : "";
}
async function authenticatedUser(request) {
  const token = bearerToken(request);
  if (!token) return "";
  const sessions = await readSessions();
  const session = sessions.find((entry) => entry && entry.token === token);
  return session ? session.username : "";
}
async function requireUser(request, response) {
  const username = await authenticatedUser(request);
  if (!username) { send(response, 401, { error: "Sign in to continue." }); return ""; }
  return username;
}
async function readRequestBuffer(request, limit) {
  return new Promise((resolve, reject) => {
    let size = 0; const chunks = [];
    request.on("data", (chunk) => { size += chunk.length; if (size > limit) { reject(new Error("Request is too large.")); request.destroy(); return; } chunks.push(chunk); });
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
  });
}
async function jsonBody(request) { return JSON.parse((await readRequestBuffer(request, maxRequestBytes * 2)).toString("utf8")); }
function postFile(id) { return path.join(dataDirectory, `${id}.json`); }
function validPostId(id) { return /^[a-f0-9-]{36}$/i.test(id || ""); }
function summarize(record) {
  return { id: record.id, type: record.type, title: record.title, author: record.author, bodyPreview: (record.body || "").slice(0, 120), mediaUrl: record.mediaUrl || "", createdAt: record.createdAt, hasLevel: !!record.hasLevel, spawnCount: Number(record.spawnCount) || 0, commentCount: Array.isArray(record.comments) ? record.comments.length : 0, likeCount: Array.isArray(record.likes) ? record.likes.length : 0 };
}
async function writeRecord(record) {
  await writeJson(postFile(record.id), record);
  const posts = await readPosts();
  const index = posts.findIndex((post) => post && post.id === record.id);
  if (index >= 0) posts[index] = summarize(record); else posts.push(summarize(record));
  await writeJson(postsIndexPath, posts);
}
async function getRecord(id) {
  if (!validPostId(id)) return null;
  try {
    const record = JSON.parse(await fs.readFile(postFile(id), "utf8"));
    record.comments = Array.isArray(record.comments) ? record.comments : [];
    record.likes = Array.isArray(record.likes) ? [...new Set(record.likes.filter((username) => typeof username === "string"))] : [];
    record.favorites = Array.isArray(record.favorites) ? [...new Set(record.favorites.filter((username) => typeof username === "string"))] : [];
    return record;
  } catch { return null; }
}
function validateLevelJson(json) {
  if (typeof json !== "string" || Buffer.byteLength(json, "utf8") > maxRequestBytes) return "Level JSON is missing or too large.";
  let level; try { level = JSON.parse(json); } catch { return "Level JSON is invalid."; }
  if (!level || typeof level !== "object" || Number(level.schemaVersion) !== 1) return "Unsupported level schema.";
  if (!Array.isArray(level.enemySpawns) || level.enemySpawns.length > maxSpawns) return `A level must contain 0 to ${maxSpawns} enemy spawns.`;
  for (const spawn of level.enemySpawns) {
    if (!spawn || !knownEnemies.has(spawn.enemy)) return "Level contains an unknown enemy.";
    if (!Number.isFinite(Number(spawn.time)) || Number(spawn.time) < 0 || Number(spawn.time) > 7200) return "Level contains an invalid spawn time.";
    if (!Number.isInteger(spawn.lane) || spawn.lane < 1 || spawn.lane > 5) return "Level contains an invalid lane.";
  }
  return null;
}
async function createSession(username) {
  const sessions = await readSessions(); const token = crypto.randomBytes(32).toString("hex");
  sessions.push({ token, username, createdAt: new Date().toISOString() });
  await writeJson(sessionsPath, sessions.slice(-2000));
  return token;
}
async function register(request, response) {
  let payload; try { payload = await jsonBody(request); } catch { send(response, 400, { error: "Request body is invalid." }); return; }
  const username = typeof payload.username === "string" ? payload.username.trim() : "";
  const password = typeof payload.password === "string" ? payload.password : "";
  const error = usernameError(username) || (password.length < 6 ? "Password must be at least 6 characters." : "");
  if (error) { send(response, 400, { error }); return; }
  const accounts = await readAccounts();
  if (accounts.some((account) => account && account.username.toLowerCase() === username.toLowerCase())) { send(response, 409, { error: "Username is already taken." }); return; }
  const salt = crypto.randomBytes(16).toString("hex");
  accounts.push({ username, salt, passwordHash: hashPassword(password, salt), createdAt: new Date().toISOString() });
  await writeJson(accountsPath, accounts);
  send(response, 201, { username, token: await createSession(username) });
}
async function login(request, response) {
  let payload; try { payload = await jsonBody(request); } catch { send(response, 400, { error: "Request body is invalid." }); return; }
  const username = typeof payload.username === "string" ? payload.username.trim() : "";
  const password = typeof payload.password === "string" ? payload.password : "";
  const account = (await readAccounts()).find((entry) => entry && entry.username.toLowerCase() === username.toLowerCase());
  if (!account || !safeEqual(hashPassword(password, account.salt), account.passwordHash)) { send(response, 401, { error: "Invalid username or password." }); return; }
  send(response, 200, { username: account.username, token: await createSession(account.username) });
}
async function listPosts(response, username = "") {
  const posts = (await readPosts()).filter((post) => post && postTypes.has(post.type) && (!username || post.author === username));
  posts.sort((left, right) => String(right.createdAt).localeCompare(String(left.createdAt)));
  send(response, 200, { posts });
}
async function getPost(request, response, id) {
  const record = await getRecord(id);
  if (!record) { send(response, 404, { error: "Post not found." }); return; }
  const username = await authenticatedUser(request);
  send(response, 200, { ...record, likes: undefined, favorites: undefined, likeCount: record.likes.length, likedByCurrentUser: !!username && record.likes.includes(username), favoritedByCurrentUser: !!username && record.favorites.includes(username) });
}
async function publishPost(request, response) {
  const username = await requireUser(request, response); if (!username) return;
  let payload; try { payload = await jsonBody(request); } catch (error) { send(response, 400, { error: error.message || "Request body is invalid." }); return; }
  const type = typeof payload.type === "string" ? payload.type.trim().toLowerCase() : "";
  if (!postTypes.has(type)) { send(response, 400, { error: "Post type must be image or level." }); return; }
  const hasLevel = typeof payload.level === "string" && payload.level.trim().length > 0;
  if (type === "level" && !hasLevel) { send(response, 400, { error: "Level posts need an attached level JSON." }); return; }
  if (hasLevel) { const error = validateLevelJson(payload.level); if (error) { send(response, 400, { error }); return; } }
  const record = { id: crypto.randomUUID(), type, title: cleanText(payload.title, type === "level" ? "Untitled Level" : "Untitled Post", 80), author: username, body: cleanBody(payload.body), mediaUrl: cleanMediaUrl(payload.mediaUrl), createdAt: new Date().toISOString(), hasLevel, spawnCount: hasLevel ? JSON.parse(payload.level).enemySpawns.length : 0, level: hasLevel ? payload.level : "", comments: [], likes: [], favorites: [] };
  if (!record.body && !record.mediaUrl && !record.hasLevel) { send(response, 400, { error: "Write something or attach an image." }); return; }
  await writeRecord(record); send(response, 201, { id: record.id, message: "Post published." });
}
async function editPost(request, response, id) {
  const username = await requireUser(request, response); if (!username) return;
  const record = await getRecord(id);
  if (!record) { send(response, 404, { error: "Post not found." }); return; }
  if (record.author !== username) { send(response, 403, { error: "You can only edit your own posts." }); return; }
  let payload; try { payload = await jsonBody(request); } catch { send(response, 400, { error: "Request body is invalid." }); return; }
  const body = cleanBody(payload.body);
  const mediaUrl = cleanMediaUrl(payload.mediaUrl);
  if (!body && !mediaUrl && !record.hasLevel) { send(response, 400, { error: "Write something or attach an image." }); return; }
  record.title = cleanText(payload.title, record.title || "Untitled Post", 80);
  record.body = body;
  record.mediaUrl = mediaUrl;
  record.updatedAt = new Date().toISOString();
  await writeRecord(record);
  send(response, 200, { id: record.id, message: "Post updated." });
}
async function toggleLike(request, response, id) {
  const username = await requireUser(request, response); if (!username) return;
  const record = await getRecord(id);
  if (!record) { send(response, 404, { error: "Post not found." }); return; }
  const index = record.likes.indexOf(username);
  let liked;
  if (index >= 0) { record.likes.splice(index, 1); liked = false; } else { record.likes.push(username); liked = true; }
  await writeRecord(record);
  send(response, 200, { liked, likeCount: record.likes.length });
}
async function listFavorites(request, response) {
  const username = await requireUser(request, response); if (!username) return;
  const summaries = await readPosts();
  const records = await Promise.all(summaries.map((summary) => getRecord(summary && summary.id)));
  const posts = records.filter((record) => record && record.favorites.includes(username)).map(summarize);
  posts.sort((left, right) => String(right.createdAt).localeCompare(String(left.createdAt)));
  send(response, 200, { posts });
}
async function toggleFavorite(request, response, id) {
  const username = await requireUser(request, response); if (!username) return;
  const record = await getRecord(id);
  if (!record) { send(response, 404, { error: "Post not found." }); return; }
  const index = record.favorites.indexOf(username);
  let favorited;
  if (index >= 0) { record.favorites.splice(index, 1); favorited = false; } else { record.favorites.push(username); favorited = true; }
  await writeRecord(record);
  send(response, 200, { favorited });
}
async function deletePost(request, response, id) {
  const username = await requireUser(request, response); if (!username) return;
  const record = await getRecord(id);
  if (!record) { send(response, 404, { error: "Post not found." }); return; }
  if (record.author !== username) { send(response, 403, { error: "You can only delete your own posts." }); return; }
  await fs.unlink(postFile(id));
  await writeJson(postsIndexPath, (await readPosts()).filter((post) => post && post.id !== id));
  send(response, 200, { message: "Post deleted." });
}
async function listComments(response, id) { const record = await getRecord(id); if (!record) send(response, 404, { error: "Post not found." }); else send(response, 200, { comments: record.comments || [] }); }
async function addComment(request, response, id) {
  const username = await requireUser(request, response); if (!username) return;
  const record = await getRecord(id); if (!record) { send(response, 404, { error: "Post not found." }); return; }
  let payload; try { payload = await jsonBody(request); } catch { send(response, 400, { error: "Request body is invalid." }); return; }
  const body = cleanComment(payload.body); if (!body) { send(response, 400, { error: "Comment cannot be empty." }); return; }
  const comment = { id: crypto.randomUUID(), author: username, body, createdAt: new Date().toISOString() };
  record.comments = Array.isArray(record.comments) ? record.comments : []; record.comments.push(comment);
  await writeRecord(record); send(response, 201, { comment });
}
async function uploadImage(request, response) {
  const username = await requireUser(request, response); if (!username) return;
  const contentType = String(request.headers["content-type"] || "").split(";", 1)[0].trim().toLowerCase();
  const extension = imageContentTypes.get(contentType); if (!extension) { send(response, 400, { error: "Image must be PNG, JPG, or WEBP." }); return; }
  let image; try { image = await readRequestBuffer(request, maxImageBytes); } catch (error) { send(response, 400, { error: error.message || "Image upload failed." }); return; }
  if (!image.length) { send(response, 400, { error: "Image is empty." }); return; }
  const fileName = `${crypto.randomUUID()}${extension}`; await fs.writeFile(path.join(uploadsDirectory, fileName), image);
  send(response, 201, { mediaUrl: `/uploads/${fileName}` });
}
async function serveUpload(response, fileName) {
  if (!/^[a-f0-9-]{36}\.(png|jpg|webp)$/i.test(fileName)) { send(response, 404, { error: "Image not found." }); return; }
  try {
    const extension = path.extname(fileName).toLowerCase(); const contentType = extension === ".png" ? "image/png" : extension === ".webp" ? "image/webp" : "image/jpeg";
    const image = await fs.readFile(path.join(uploadsDirectory, fileName));
    response.writeHead(200, { "Content-Type": contentType, "Content-Length": image.length, "Cache-Control": "public, max-age=31536000, immutable", "Access-Control-Allow-Origin": corsOrigin }); response.end(image);
  } catch (error) { console.error("Image read failed", fileName, error); send(response, 404, { error: "Image not found." }); }
}

const server = http.createServer(async (request, response) => {
  if (request.method === "OPTIONS") { response.writeHead(204, { "Access-Control-Allow-Origin": corsOrigin, "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, OPTIONS", "Access-Control-Allow-Headers": "Content-Type, Authorization" }); response.end(); return; }
  const url = new URL(request.url, `http://${request.headers.host || "localhost"}`);
  try {
    if (request.method === "GET" && url.pathname === "/health") send(response, 200, { ok: true });
    else if (request.method === "POST" && url.pathname === "/api/auth/register") await register(request, response);
    else if (request.method === "POST" && url.pathname === "/api/auth/login") await login(request, response);
    else if (request.method === "GET" && url.pathname === "/api/auth/me") { const username = await authenticatedUser(request); username ? send(response, 200, { username }) : send(response, 401, { error: "Not signed in." }); }
    else if (request.method === "GET" && url.pathname === "/api/me/posts") { const username = await requireUser(request, response); if (username) await listPosts(response, username); }
    else if (request.method === "GET" && url.pathname === "/api/me/favorites") await listFavorites(request, response);
    else if (request.method === "GET" && url.pathname === "/api/posts") await listPosts(response);
    else if (request.method === "POST" && url.pathname === "/api/posts") await publishPost(request, response);
    else if (request.method === "GET" && url.pathname.match(/^\/api\/posts\/[^/]+\/comments$/)) await listComments(response, decodeURIComponent(url.pathname.split("/")[3]));
    else if (request.method === "POST" && url.pathname.match(/^\/api\/posts\/[^/]+\/comments$/)) await addComment(request, response, decodeURIComponent(url.pathname.split("/")[3]));
    else if (request.method === "DELETE" && url.pathname.startsWith("/api/posts/")) await deletePost(request, response, decodeURIComponent(url.pathname.slice("/api/posts/".length)));
    else if (request.method === "POST" && url.pathname.match(/^\/api\/posts\/[^/]+\/like$/)) await toggleLike(request, response, decodeURIComponent(url.pathname.split("/")[3]));
    else if (request.method === "POST" && url.pathname.match(/^\/api\/posts\/[^/]+\/favorite$/)) await toggleFavorite(request, response, decodeURIComponent(url.pathname.split("/")[3]));
    else if (request.method === "PUT" && url.pathname.startsWith("/api/posts/")) await editPost(request, response, decodeURIComponent(url.pathname.slice("/api/posts/".length)));
    else if (request.method === "GET" && url.pathname.startsWith("/api/posts/")) await getPost(request, response, decodeURIComponent(url.pathname.slice("/api/posts/".length)));
    else if (request.method === "POST" && url.pathname === "/api/uploads") await uploadImage(request, response);
    else if (request.method === "GET" && url.pathname.startsWith("/uploads/")) await serveUpload(response, path.basename(url.pathname));
    else send(response, 404, { error: "Not found." });
  } catch (error) { console.error(error); send(response, 500, { error: "Server error." }); }
});

ensureStorage().then(() => server.listen(port, process.env.HOST || "::", () => console.log(`Bullet Foundry community API listening on ${process.env.HOST || "::"}:${port}`))).catch((error) => { console.error("Could not initialize community API storage.", error); process.exit(1); });
