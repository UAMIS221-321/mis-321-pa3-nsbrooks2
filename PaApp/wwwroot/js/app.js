const sessionListEl = document.getElementById("sessionList");
const messagesEl = document.getElementById("messages");
const composerEl = document.getElementById("composer");
const inputEl = document.getElementById("input");
const statusEl = document.getElementById("status");
const toastHostEl = document.getElementById("toastHost");
const newChatEl = document.getElementById("newChat");
const sendEl = document.getElementById("send");
const stageTrailsEl = document.getElementById("stageTrails");
const stageWeatherEl = document.getElementById("stageWeather");
const stageGearEl = document.getElementById("stageGear");
const stageItineraryEl = document.getElementById("stageItinerary");

const storyStripEl = document.getElementById("storyStrip");
const feedStreamEl = document.getElementById("feedStream");
const exploreGridEl = document.getElementById("exploreGrid");
const profilePanelEl = document.getElementById("profilePanel");
const profilePickerEl = document.getElementById("profilePicker");
const profilePickerListEl = document.getElementById("profilePickerList");
const openPickerEl = document.getElementById("openPicker");

let activeSessionId = null;
let thinkingBubbleEl = null;

/** @type {any | null} */
let meProfile = null;
/** @type {string | null} */
let browseProfileId = null;
let exploreLoaded = false;

/** @type {RegExp} */
const URL_IN_TEXT = /https?:\/\/[^\s<>"']+/gi;

function clipTrailingPunctuation(url) {
  let s = url;
  while (s.length > 0 && /[),.;:!?]+$/.test(s)) {
    s = s.slice(0, -1);
  }
  return s;
}

function showToast(message, variant = "err") {
  if (!toastHostEl || !message) return;
  const kind = variant === "ok" ? "ok" : variant === "err" ? "err" : "notice";
  const wrap = document.createElement("div");
  wrap.className = `toast ${kind}`;
  wrap.setAttribute("role", "status");
  const title = document.createElement("div");
  title.className = "toast-title";
  title.textContent =
    variant === "err" ? "Something went wrong" : variant === "ok" ? "Done" : "Notice";
  const body = document.createElement("div");
  body.textContent = message;
  wrap.appendChild(title);
  wrap.appendChild(body);
  toastHostEl.appendChild(wrap);
  setTimeout(() => {
    wrap.remove();
  }, 8000);
}

function fillMessageBody(bodyEl, text, linkify) {
  bodyEl.textContent = "";
  const s = text ?? "";
  if (!linkify) {
    bodyEl.textContent = s;
    return;
  }
  let last = 0;
  for (const m of s.matchAll(URL_IN_TEXT)) {
    const start = m.index ?? 0;
    if (start > last) {
      bodyEl.appendChild(document.createTextNode(s.slice(last, start)));
    }
    const raw = m[0];
    const clipped = clipTrailingPunctuation(raw);
    try {
      const u = new URL(clipped);
      if (u.protocol !== "http:" && u.protocol !== "https:") {
        bodyEl.appendChild(document.createTextNode(raw));
      } else {
        const a = document.createElement("a");
        a.href = u.toString();
        a.textContent = clipped;
        a.className = "bubble-link";
        a.target = "_blank";
        a.rel = "noopener noreferrer";
        bodyEl.appendChild(a);
        if (raw.length > clipped.length) {
          bodyEl.appendChild(document.createTextNode(raw.slice(clipped.length)));
        }
      }
    } catch {
      bodyEl.appendChild(document.createTextNode(raw));
    }
    last = start + m[0].length;
  }
  bodyEl.appendChild(document.createTextNode(s.slice(last)));
}

function showThinkingBubble() {
  removeThinkingBubble();
  const wrap = document.createElement("div");
  wrap.className = "bubble scout thinking";
  wrap.setAttribute("aria-live", "polite");
  const rl = document.createElement("div");
  rl.className = "role";
  rl.textContent = "Scout";
  const b = document.createElement("div");
  b.className = "body thinking-pulse";
  b.textContent = "Working… (web search + tools can take 10–20s)";
  wrap.appendChild(rl);
  wrap.appendChild(b);
  messagesEl.appendChild(wrap);
  thinkingBubbleEl = wrap;
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function removeThinkingBubble() {
  thinkingBubbleEl?.remove();
  thinkingBubbleEl = null;
}

async function api(path, options = {}) {
  const res = await fetch(path, {
    credentials: "include",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    ...options,
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = { raw: text };
  }
  if (!res.ok) {
    const msg =
      (typeof data?.error === "string" ? data.error : null) ??
      data?.title ??
      data?.detail ??
      (typeof data?.errors === "object" ? JSON.stringify(data.errors) : null) ??
      text ??
      res.statusText;
    throw new Error(typeof msg === "string" ? msg : JSON.stringify(msg));
  }
  return data;
}

function setStatus(text) {
  statusEl.textContent = text ?? "";
}

function renderBubble(role, content) {
  const wrap = document.createElement("div");
  const r = role === "assistant" ? "scout" : "user";
  wrap.className = `bubble ${r}`;
  const rl = document.createElement("div");
  rl.className = "role";
  rl.textContent = role === "assistant" ? "Scout" : "You";
  const b = document.createElement("div");
  b.className = "body";
  fillMessageBody(b, content, role === "assistant");
  wrap.appendChild(rl);
  wrap.appendChild(b);
  messagesEl.appendChild(wrap);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function esc(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function renderStage(stage) {
  if (!stage) {
    stageTrailsEl.innerHTML = '<p class="placeholder">No trail query yet.</p>';
    stageWeatherEl.innerHTML = '<p class="placeholder mono">No weather pull yet.</p>';
    stageGearEl.innerHTML = '<p class="placeholder">No gear list persisted.</p>';
    stageItineraryEl.innerHTML = '<p class="placeholder">No itinerary issued.</p>';
    return;
  }

  if (stage.trails?.results?.length) {
    const rows = stage.trails.results
      .map(
        (t) => `
      <div class="trail-row">
        <div class="trail-name mono">${esc(t.name)} <span class="trail-meta">· ${esc(t.difficulty)} · ${t.elevationGainFt} ft</span></div>
        <div class="trail-meta mono">${esc(t.region)} · ${t.dogFriendly ? "dog-friendly" : "no dogs"} · ${esc(t.crowdCalendarNote || "")}</div>
        <div class="mono" style="margin-top:0.25rem;color:#a5b5ad;">${esc(t.excerpt || "")}</div>
      </div>`
      )
      .join("");
    stageTrailsEl.innerHTML = rows;
  } else if (stage.trails) {
    stageTrailsEl.innerHTML = '<p class="placeholder mono">Query returned no rows — widen filters.</p>';
  }

  if (stage.weather?.hourly?.length) {
    const w = stage.weather;
    const head = `<div class="weather-head mono">${esc(w.label || "Forecast")} · ${esc(w.source || "")}</div>`;
    const hrs = w.hourly.slice(0, 12);
    const tb = [
      "<table class='weather-table mono'><thead><tr><th>Time (UTC)</th><th>°F</th><th>Precip%</th><th>Wind</th><th>WMO</th></tr></thead><tbody>",
      ...hrs.map(
        (h) =>
          `<tr><td>${esc(h.time)}</td><td>${h.tempF}</td><td>${h.precipPct}</td><td>${h.windMph}</td><td>${h.wmoCode}</td></tr>`
      ),
      "</tbody></table>",
    ].join("");
    stageWeatherEl.innerHTML = head + tb;
  }

  if (stage.gear?.items?.length) {
    const items = stage.gear.items.map((x) => `<li>${esc(x)}</li>`).join("");
    stageGearEl.innerHTML = `<ul class="gear-list">${items}</ul><p class="trail-meta mono">Tag: ${esc(stage.gear.difficultyTag || "—")}</p>`;
  }

  if (stage.itinerary?.viewerPath) {
    const path = stage.itinerary.viewerPath;
    const url = path.startsWith("http") ? path : `${window.location.origin}${path}`;
    stageItineraryEl.innerHTML = `
      <p class="mono"><strong>${esc(stage.itinerary.trailName || "Itinerary")}</strong></p>
      <p class="trail-meta">${esc(stage.itinerary.summaryLine || "")}</p>
      <p class="mono" style="margin-top:0.5rem;">Contact link: <a class="itin-link" href="${esc(url)}" target="_blank" rel="noopener">${esc(url)}</a></p>`;
  }
}

async function loadStage(sessionId) {
  try {
    const data = await api(`/api/chat/sessions/${sessionId}/stage`);
    renderStage(data.stage);
  } catch {
    renderStage(null);
  }
}

async function loadSessions() {
  const sessions = await api("/api/chat/sessions");
  sessionListEl.innerHTML = "";
  for (const s of sessions) {
    const sid = s.id ?? s.Id;
    const li = document.createElement("li");
    li.textContent = s.title ?? s.Title;
    li.dataset.id = sid;
    if (sid === activeSessionId) li.classList.add("active");
    li.addEventListener("click", () => selectSession(sid, s.title ?? s.Title));
    sessionListEl.appendChild(li);
  }
}

async function selectSession(id, title) {
  activeSessionId = id;
  [...sessionListEl.querySelectorAll("li")].forEach((li) => {
    li.classList.toggle("active", li.dataset.id === id);
  });
  messagesEl.innerHTML = "";
  setStatus("Loading briefing…");
  try {
    const msgs = await api(`/api/chat/sessions/${id}/messages`);
    setStatus("");
    for (const m of msgs) renderBubble(m.role, m.content);
    await loadStage(id);
  } catch (err) {
    setStatus("");
    showToast(String(err.message ?? err));
  }
}

function sessionIdFromCreateResponse(created) {
  if (!created) return null;
  return created.id ?? created.Id ?? null;
}

async function createSession() {
  setStatus("Opening new briefing…");
  try {
    const created = await api("/api/chat/sessions", {
      method: "POST",
      body: JSON.stringify({ title: "New briefing" }),
    });
    const sid = sessionIdFromCreateResponse(created);
    if (!sid) {
      const msg =
        "Server returned no session id. Check browser devtools → Network for /api/chat/sessions.";
      setStatus(msg);
      showToast(msg);
      return;
    }
    await loadSessions();
    await selectSession(sid, created.title ?? created.Title ?? "New briefing");
    setStatus("");
    inputEl.focus();
  } catch (err) {
    const msg = String(err.message ?? err);
    setStatus(msg);
    showToast(msg);
  }
}

inputEl.addEventListener("keydown", (e) => {
  if (e.key !== "Enter" || e.shiftKey) return;
  if (e.isComposing) return;
  e.preventDefault();
  if (sendEl.disabled) return;
  composerEl.requestSubmit();
});

composerEl.addEventListener("submit", async (e) => {
  e.preventDefault();
  if (!activeSessionId) {
    const msg = "Select or create a briefing first.";
    setStatus(msg);
    showToast(msg);
    return;
  }
  const text = inputEl.value.trim();
  if (!text) return;

  sendEl.disabled = true;
  composerEl.setAttribute("aria-busy", "true");
  setStatus("Scout is thinking…");
  renderBubble("user", text);
  inputEl.value = "";
  showThinkingBubble();

  try {
    const data = await api(`/api/chat/sessions/${activeSessionId}/messages`, {
      method: "POST",
      body: JSON.stringify({ text }),
    });
    removeThinkingBubble();
    renderBubble("assistant", data.reply);
    renderStage(data.stage);
    setStatus("");
    await loadSessions();
  } catch (err) {
    removeThinkingBubble();
    const msg = String(err.message ?? err);
    setStatus(msg);
    showToast(msg);
  } finally {
    sendEl.disabled = false;
    composerEl.removeAttribute("aria-busy");
  }
});

newChatEl.addEventListener("click", () => createSession());

/* ---- Social (profiles + feed) ---- */

function avatarLetter(displayName) {
  const t = (displayName || "?").trim();
  return t ? t[0].toUpperCase() : "?";
}

function avatarStyle(hue) {
  const h = Number.isFinite(hue) ? hue : 160;
  return `background:linear-gradient(145deg,hsl(${h},62%,48%),hsl(${(h + 48) % 360},55%,32%));`;
}

function trailHeroStyle(trailId, region) {
  const n = Number(trailId) || 0;
  const h = (n * 47 + (region || "").length * 3) % 360;
  return `background:linear-gradient(165deg,hsl(${h},45%,38%),hsl(${(h + 70) % 360},40%,22%));`;
}

async function refreshMe() {
  const data = await api("/api/social/me");
  meProfile = data.profile ?? null;
  return meProfile;
}

function openProfilePicker() {
  profilePickerEl.hidden = false;
}

function closeProfilePicker() {
  profilePickerEl.hidden = true;
}

async function populateProfilePicker() {
  const rows = await api("/api/social/profiles");
  profilePickerListEl.innerHTML = "";
  for (const p of rows) {
    const id = p.id ?? p.Id;
    const li = document.createElement("li");
    const hue = p.avatarHue ?? 200;
    li.innerHTML = `
      <span class="ss-avatar" style="${avatarStyle(hue)}width:40px;height:40px;">${esc(avatarLetter(p.displayName))}</span>
      <span style="flex:1;min-width:0">
        <strong>${esc(p.displayName)}</strong><br />
        <span style="font-size:0.78rem;color:#737373">@${esc(p.handle)}</span>
      </span>`;
    li.addEventListener("click", async () => {
      try {
        const res = await api("/api/social/session", {
          method: "POST",
          body: JSON.stringify({ profileId: id }),
        });
        meProfile = res.profile;
        closeProfilePicker();
        browseProfileId = null;
        await renderStories();
        await loadHomeFeed();
        showToast(`Signed in as @${meProfile.handle}`, "ok");
      } catch (err) {
        showToast(String(err.message ?? err));
      }
    });
    profilePickerListEl.appendChild(li);
  }
}

async function renderStories() {
  const rows = await api("/api/social/profiles");
  storyStripEl.innerHTML = "";
  for (const p of rows) {
    const id = p.id ?? p.Id;
    const hue = p.avatarHue ?? 160;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "ss-story";
    btn.innerHTML = `
      <div class="ss-story-ring">
        <div class="ss-story-ring-inner" style="${avatarStyle(hue)}">${esc(avatarLetter(p.displayName))}</div>
      </div>
      <div class="ss-story-handle">@${esc(p.handle)}</div>`;
    btn.addEventListener("click", () => {
      browseProfileId = id;
      loadProfilePanel(id);
      switchTab("profile");
    });
    storyStripEl.appendChild(btn);
  }
}

function formatTime(iso) {
  try {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
  } catch {
    return "";
  }
}

async function loadHomeFeed() {
  if (!meProfile) {
    feedStreamEl.innerHTML =
      '<p style="padding:1rem;color:#525252;font-size:0.9rem;">Choose a profile to load your personalized trail feed.</p>';
    return;
  }
  try {
    const posts = await api("/api/social/feed?take=40");
    if (!posts.length) {
      feedStreamEl.innerHTML =
        '<p style="padding:1rem;color:#525252;">No posts yet. Follow hikers or check back after seed data is loaded.</p>';
      return;
    }
    feedStreamEl.innerHTML = posts
      .map((post) => {
        const t = post.trail;
        const trailBlock = t
          ? `<div class="ss-trail-card">
          <div class="ss-trail-photo" style="${trailHeroStyle(t.id, t.region)}">
            <div class="ss-trail-label">${esc(t.name)}</div>
          </div>
          <div class="ss-trail-body">
            <div class="ss-trail-meta mono">${esc(t.region)} · ${esc(t.difficulty)} · ${t.elevationGainFt} ft gain · ${t.lengthMi} mi · ${t.dogFriendly ? "dogs ok" : "no dogs"}</div>
            <div class="ss-trail-excerpt">${esc(t.excerpt || "")}</div>
          </div>
        </div>`
          : "";
        return `<article class="ss-post">
        <div class="ss-post-head">
          <div class="ss-avatar" style="${avatarStyle(post.authorAvatarHue)}">${esc(avatarLetter(post.authorDisplayName))}</div>
          <div class="ss-post-meta">
            <div class="ss-post-user">${esc(post.authorDisplayName)}</div>
            <div class="ss-post-handle">@${esc(post.authorHandle)}</div>
          </div>
        </div>
        ${trailBlock}
        <div class="ss-post-caption">${esc(post.caption)}</div>
        <div class="ss-post-time">${esc(formatTime(post.createdAt))}</div>
      </article>`;
      })
      .join("");
  } catch (err) {
    feedStreamEl.innerHTML = `<p style="padding:1rem;color:#b91c1c;">${esc(String(err.message ?? err))}</p>`;
  }
}

async function loadExplore() {
  const trails = await api("/api/social/explore/trails?take=24");
  exploreGridEl.innerHTML = trails
    .map(
      (t) => `
    <button type="button" class="ss-explore-tile" data-trail="${esc(t.name)}">
      <div class="ss-explore-tile-visual" style="${trailHeroStyle(t.id, t.region)}"></div>
      <div class="ss-explore-tile-body">
        <h3>${esc(t.name)}</h3>
        <p>${esc(t.region)} · ${esc(t.difficulty)} · ${t.elevationGainFt} ft</p>
      </div>
    </button>`
    )
    .join("");

  exploreGridEl.querySelectorAll(".ss-explore-tile").forEach((btn) => {
    btn.addEventListener("click", () => {
      const name = btn.getAttribute("data-trail");
      switchTab("scout");
      inputEl.value = `Tell me about ${name} and whether it fits a weekday hike.`;
      inputEl.focus();
    });
  });
}

async function loadProfilePanel(profileGuid) {
  const detail = await api(`/api/social/profiles/${profileGuid}`);
  const posts = await api(`/api/social/profiles/${profileGuid}/posts?take=30`);
  const id = detail.id ?? detail.Id;
  const myId = meProfile ? String(meProfile.id ?? meProfile.Id) : "";
  const isSelf = myId !== "" && myId === String(id);

  const followBtn =
    !isSelf && meProfile
      ? detail.isFollowing
        ? `<button type="button" class="ss-btn ss-btn-ghost" id="btnUnfollow">Following</button>`
        : `<button type="button" class="ss-btn ss-btn-primary" id="btnFollow">Follow</button>`
      : "";

  const selfActions = isSelf
    ? `<button type="button" class="ss-btn ss-btn-ghost" id="btnLogout">Log out</button>`
    : "";

  profilePanelEl.innerHTML = `
    <div class="ss-profile-head">
      <div class="ss-profile-avatar-lg" style="${avatarStyle(detail.avatarHue)}">${esc(avatarLetter(detail.displayName))}</div>
      <div class="ss-profile-stats">
        <div class="ss-profile-stat"><b>${detail.posts}</b><span>posts</span></div>
        <div class="ss-profile-stat"><b>${detail.followers}</b><span>followers</span></div>
        <div class="ss-profile-stat"><b>${detail.following}</b><span>following</span></div>
      </div>
    </div>
    <div class="ss-profile-bio">
      <div class="ss-headline" style="margin-bottom:0.35rem;">@${esc(detail.handle)}</div>
      ${esc(detail.bio || "")}
    </div>
    <div class="ss-profile-actions">
      ${followBtn}
      ${selfActions}
      ${!isSelf ? `<button type="button" class="ss-btn ss-btn-ghost" id="btnBackMe">My profile</button>` : ""}
    </div>
    <div class="ss-profile-grid" id="profilePostGrid"></div>`;

  const grid = document.getElementById("profilePostGrid");
  for (const post of posts) {
    const t = post.trail;
    const cell = document.createElement("button");
    cell.type = "button";
    cell.className = "ss-profile-cell";
    cell.style.cssText = t ? trailHeroStyle(t.id, t.region) : "background:#d4d4d4;";
    cell.title = post.caption || "Post";
    cell.addEventListener("click", () => {
      if (t) {
        switchTab("scout");
        inputEl.value = `Plan a hike on ${t.name} (${t.region}).`;
        inputEl.focus();
      }
    });
    grid?.appendChild(cell);
  }

  document.getElementById("btnFollow")?.addEventListener("click", async () => {
    try {
      await api("/api/social/follow", {
        method: "POST",
        body: JSON.stringify({ followingId: id }),
      });
      await loadProfilePanel(profileGuid);
      await loadHomeFeed();
    } catch (e) {
      showToast(String(e.message ?? e));
    }
  });

  document.getElementById("btnUnfollow")?.addEventListener("click", async () => {
    try {
      await api(`/api/social/follow/${id}`, { method: "DELETE" });
      await loadProfilePanel(profileGuid);
      await loadHomeFeed();
    } catch (e) {
      showToast(String(e.message ?? e));
    }
  });

  document.getElementById("btnBackMe")?.addEventListener("click", async () => {
    if (!meProfile) return;
    browseProfileId = null;
    await loadProfilePanel(meProfile.id ?? meProfile.Id);
  });

  document.getElementById("btnLogout")?.addEventListener("click", async () => {
    try {
      await api("/api/social/logout", { method: "POST" });
      meProfile = null;
      browseProfileId = null;
      feedStreamEl.innerHTML =
        '<p style="padding:1rem;color:#525252;">Choose a profile to see your feed again.</p>';
      openProfilePicker();
      await populateProfilePicker();
      showToast("Signed out.", "ok");
    } catch (e) {
      showToast(String(e.message ?? e));
    }
  });
}

function switchTab(name) {
  document.querySelectorAll(".ss-view").forEach((el) => {
    el.classList.toggle("is-active", el.id === `view${name.charAt(0).toUpperCase() + name.slice(1)}`);
  });
  const map = { home: "Home", explore: "Explore", scout: "Scout", profile: "Profile" };
  document.querySelectorAll(".ss-nav-btn").forEach((btn) => {
    const tab = btn.getAttribute("data-ss-tab");
    const on = tab === name;
    btn.classList.toggle("is-active", on);
    if (on) btn.setAttribute("aria-current", "page");
    else btn.removeAttribute("aria-current");
  });

  if (name === "explore" && !exploreLoaded) {
    exploreLoaded = true;
    loadExplore().catch((e) => showToast(String(e.message ?? e)));
  }
  if (name === "home") {
    loadHomeFeed().catch((e) => showToast(String(e.message ?? e)));
  }
  if (name === "profile") {
    const pid = browseProfileId || (meProfile ? (meProfile.id ?? meProfile.Id) : null);
    if (pid) {
      loadProfilePanel(pid).catch((e) => showToast(String(e.message ?? e)));
    } else {
      profilePanelEl.innerHTML =
        '<p style="padding:1rem;">Pick a profile from the switcher (⇄) to see your page.</p>';
    }
  }
}

document.querySelectorAll(".ss-nav-btn").forEach((btn) => {
  btn.addEventListener("click", () => {
    const tab = btn.getAttribute("data-ss-tab");
    if (tab === "profile") {
      browseProfileId = null;
    }
    switchTab(tab);
  });
});

openPickerEl?.addEventListener("click", () => {
  populateProfilePicker().catch((e) => showToast(String(e.message ?? e)));
  openProfilePicker();
});

profilePickerEl?.addEventListener("click", (e) => {
  if (e.target === profilePickerEl) {
    closeProfilePicker();
  }
});

async function initSocial() {
  try {
    await refreshMe();
    await renderStories();
    if (!meProfile) {
      openProfilePicker();
      await populateProfilePicker();
    } else {
      await loadHomeFeed();
    }
  } catch (e) {
    showToast(`Social: ${e.message ?? e}`);
  }
}

(async function initScout() {
  try {
    const sessions = await api("/api/chat/sessions");
    if (sessions.length === 0) {
      await createSession();
    } else {
      await loadSessions();
      const s0 = sessions[0];
      await selectSession(s0.id ?? s0.Id, s0.title ?? s0.Title);
    }
  } catch (e) {
    const msg = `Scout API: ${e.message ?? e}`;
    setStatus(msg);
    showToast(msg);
  }
})();

initSocial();
