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
const legacyLevelsIndexPath = path.join(dataDirectory, "levels.json");
const maxRequestBytes = 128 * 1024;
const maxImageBytes = 6 * 1024 * 1024;
const maxPostBodyLength = 4000;
const maxSpawns = 1000;
const postTypes = new Set(["image", "level"]);
const imageContentTypes = new Map([
  ["image/png", ".png"],
  ["image/jpeg", ".jpg"],
  ["image/webp", ".webp"]
]);
const knownEnemies = new Set([
  "Goblin",
  "SpeedGoblin",
  "Barbarian",
  "PigLeader",
  "FrogPrincess",
  "Chicken",
  "Giant"
]);

async function ensureStorage() {
  await fs.mkdir(dataDirectory, { recursive: true });
  await fs.mkdir(uploadsDirectory, { recursive: true });
  try {
    await fs.access(postsIndexPath);
  } catch {
    await writeJson(postsIndexPath, []);
  }

  // Convert the first version's standalone level posts on server upgrade.
  const posts = await readPosts();
  if (posts.length > 0) return;
  try {
    const legacy = JSON.parse(await fs.readFile(legacyLevelsIndexPath, "utf8"));
    if (!Array.isArray(legacy) || legacy.length === 0) return;
    const migrated = legacy.map((level) => ({
      id: level.id,
      type: "level",
      title: level.name || "Untitled Level",
      author: level.author || "Anonymous",
      bodyPreview: "A playable community level.",
      mediaUrl: "",
      createdAt: level.createdAt || new Date().toISOString(),
      hasLevel: true,
      spawnCount: Number(level.spawnCount) || 0
    }));
    await writeJson(postsIndexPath, migrated);
  } catch {
    // No previous server data exists.
  }
}

async function readPosts() {
  try {
    const contents = await fs.readFile(postsIndexPath, "utf8");
    const posts = JSON.parse(contents);
    return Array.isArray(posts) ? posts : [];
  } catch {
    return [];
  }
}

async function writeJson(filePath, value) {
  const temporaryPath = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  await fs.writeFile(temporaryPath, JSON.stringify(value, null, 2), "utf8");
  await fs.rename(temporaryPath, filePath);
}

function send(response, status, body) {
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": corsOrigin,
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type"
  });
  response.end(JSON.stringify(body));
}

function cleanText(value, fallback, maximumLength) {
  const cleaned = typeof value === "string" ? value.trim().replace(/\s+/g, " ") : "";
  return (cleaned || fallback).slice(0, maximumLength);
}

function cleanBody(value) {
  return typeof value === "string" ? value.trim().slice(0, maxPostBodyLength) : "";
}

function cleanMediaUrl(value) {
  if (typeof value !== "string" || value.length > 2048) return "";
  try {
    const url = new URL(value.trim());
    return url.protocol === "http:" || url.protocol === "https:" ? url.href : "";
  } catch {
    return "";
  }
}

function validateLevelJson(json) {
  if (typeof json !== "string" || Buffer.byteLength(json, "utf8") > maxRequestBytes) {
    return "Level JSON is missing or too large.";
  }

  let level;
  try {
    level = JSON.parse(json);
  } catch {
    return "Level JSON is invalid.";
  }

  if (!level || typeof level !== "object" || Number(level.schemaVersion) !== 1) {
    return "Unsupported level schema.";
  }

  if (!Array.isArray(level.enemySpawns) || level.enemySpawns.length > maxSpawns) {
    return `A level must contain 0 to ${maxSpawns} enemy spawns.`;
  }

  for (const spawn of level.enemySpawns) {
    if (!spawn || !knownEnemies.has(spawn.enemy)) return "Level contains an unknown enemy.";
    if (!Number.isFinite(Number(spawn.time)) || Number(spawn.time) < 0 || Number(spawn.time) > 7200) {
      return "Level contains an invalid spawn time.";
    }
    if (!Number.isInteger(spawn.lane) || spawn.lane < 1 || spawn.lane > 5) {
      return "Level contains an invalid lane.";
    }
  }

  return null;
}

function readRequestBuffer(request, limit) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    request.on("data", (chunk) => {
      size += chunk.length;
      if (size > limit) {
        reject(new Error("Request is too large."));
        request.destroy();
        return;
      }
      chunks.push(chunk);
    });
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
  });
}

async function readRequestBody(request) {
  return (await readRequestBuffer(request, maxRequestBytes * 2)).toString("utf8");
}

function summarize(record) {
  return {
    id: record.id,
    type: record.type,
    title: record.title,
    author: record.author,
    bodyPreview: record.body.slice(0, 120),
    mediaUrl: record.mediaUrl,
    createdAt: record.createdAt,
    hasLevel: record.hasLevel,
    spawnCount: record.spawnCount
  };
}

async function listPosts(response) {
  const posts = (await readPosts()).filter((post) => post && postTypes.has(post.type));
  posts.sort((left, right) => String(right.createdAt).localeCompare(String(left.createdAt)));
  send(response, 200, { posts });
}

async function getPost(response, id) {
  if (!/^[a-f0-9-]{36}$/i.test(id)) {
    send(response, 404, { error: "Post not found." });
    return;
  }

  try {
    const record = JSON.parse(await fs.readFile(path.join(dataDirectory, `${id}.json`), "utf8"));
    // Normalize records written by the first level-only server version.
    if (!record.type && record.level) {
      record.type = "level";
      record.title = record.name || "Untitled Level";
      record.body = "A playable community level.";
      record.mediaUrl = "";
      record.hasLevel = true;
    }
    send(response, 200, record);
  } catch {
    send(response, 404, { error: "Post not found." });
  }
}

async function publishPost(request, response) {
  let payload;
  try {
    payload = JSON.parse(await readRequestBody(request));
  } catch (error) {
    send(response, 400, { error: error.message || "Request body is invalid." });
    return;
  }

  const type = typeof payload.type === "string" ? payload.type.trim().toLowerCase() : "";
  if (!postTypes.has(type)) {
    send(response, 400, { error: "Post type must be image or level." });
    return;
  }

  const mediaUrl = cleanMediaUrl(payload.mediaUrl);
  if (type === "image" && !mediaUrl) {
    send(response, 400, { error: "Image posts need an uploaded image URL." });
    return;
  }

  const hasLevel = typeof payload.level === "string" && payload.level.trim().length > 0;
  if (type === "level" && !hasLevel) {
    send(response, 400, { error: "Level posts need an attached level JSON." });
    return;
  }

  if (hasLevel) {
    const levelError = validateLevelJson(payload.level);
    if (levelError) {
      send(response, 400, { error: levelError });
      return;
    }
  }

  const level = hasLevel ? payload.level : "";
  const id = crypto.randomUUID();
  const record = {
    id,
    type,
    title: cleanText(payload.title, type === "level" ? "Untitled Level" : "Untitled Post", 80),
    author: cleanText(payload.author, "Anonymous", 48),
    body: cleanBody(payload.body),
    mediaUrl,
    createdAt: new Date().toISOString(),
    hasLevel,
    spawnCount: hasLevel ? JSON.parse(level).enemySpawns.length : 0,
    level
  };

  const posts = await readPosts();
  posts.push(summarize(record));
  await writeJson(path.join(dataDirectory, `${id}.json`), record);
  await writeJson(postsIndexPath, posts);
  send(response, 201, { id, message: "Post published." });
}

async function uploadImage(request, response) {
  const contentType = String(request.headers["content-type"] || "").split(";", 1)[0].trim().toLowerCase();
  const extension = imageContentTypes.get(contentType);
  if (!extension) {
    send(response, 400, { error: "Image must be PNG, JPG, or WEBP." });
    return;
  }

  let image;
  try {
    image = await readRequestBuffer(request, maxImageBytes);
  } catch (error) {
    send(response, 400, { error: error.message || "Image upload failed." });
    return;
  }

  if (image.length === 0) {
    send(response, 400, { error: "Image is empty." });
    return;
  }

  const fileName = `${crypto.randomUUID()}${extension}`;
  await fs.writeFile(path.join(uploadsDirectory, fileName), image);
  send(response, 201, { mediaUrl: `/uploads/${fileName}` });
}

async function serveUpload(response, fileName) {
  if (!/^[a-f0-9-]{36}\.(png|jpg|webp)$/i.test(fileName)) {
    send(response, 404, { error: "Image not found." });
    return;
  }

  try {
    const extension = path.extname(fileName).toLowerCase();
    const contentType = extension === ".png" ? "image/png" : extension === ".webp" ? "image/webp" : "image/jpeg";
    const image = await fs.readFile(path.join(uploadsDirectory, fileName));
    response.writeHead(200, {
      "Content-Type": contentType,
      "Content-Length": image.length,
      "Cache-Control": "public, max-age=31536000, immutable",
      "Access-Control-Allow-Origin": corsOrigin
    });
    response.end(image);
  } catch {
    send(response, 404, { error: "Image not found." });
  }
}

const server = http.createServer(async (request, response) => {
  if (request.method === "OPTIONS") {
    response.writeHead(204, {
      "Access-Control-Allow-Origin": corsOrigin,
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type"
    });
    response.end();
    return;
  }

  const url = new URL(request.url, `http://${request.headers.host || "localhost"}`);
  try {
    if (request.method === "GET" && url.pathname === "/health") {
      send(response, 200, { ok: true });
    } else if (request.method === "GET" && url.pathname === "/api/posts") {
      await listPosts(response);
    } else if (request.method === "GET" && url.pathname.startsWith("/api/posts/")) {
      await getPost(response, decodeURIComponent(url.pathname.slice("/api/posts/".length)));
    } else if (request.method === "POST" && url.pathname === "/api/posts") {
      await publishPost(request, response);
    } else if (request.method === "POST" && url.pathname === "/api/uploads") {
      await uploadImage(request, response);
    } else if (request.method === "GET" && url.pathname.startsWith("/uploads/")) {
      await serveUpload(response, path.basename(url.pathname));
    } else {
      send(response, 404, { error: "Not found." });
    }
  } catch (error) {
    console.error(error);
    send(response, 500, { error: "Server error." });
  }
});

ensureStorage()
  .then(() => server.listen(port, "0.0.0.0", () => {
    console.log(`Bullet Foundry community API listening on ${port}`);
  }))
  .catch((error) => {
    console.error("Could not initialize community API storage.", error);
    process.exit(1);
  });
