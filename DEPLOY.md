# Deploy SummitScout (Heroku + cloud MySQL)

Your laptop MySQL is **only for you**. Public users need a **cloud database** Heroku can reach. Use Workbench to **connect to that cloud host** and run the same SQL files you use locally.

---

## Quick checklist (example app: `summitscout-nsbrooks-pa3`)

Run these from the **Git repo root** (folder that contains `Procfile`, `PaApp.sln`, `database/`). If you are inside `PaApp/`, run `cd ..` first.

```bash
export HEROKU_APP=summitscout-nsbrooks-pa3

# 1) MySQL on Heroku (plan slug is NOT kite-flex — see §2 Path A)
heroku addons:create jawsdb --app "$HEROKU_APP"
# Free tier is usually: jawsdb:kitefin — list plans with: heroku addons:plans jawsdb
# If JawsDB is unavailable in your region, use §2 Path B and set DATABASE_URL / MYSQL_CONNECTION_STRING

# 2) Secrets & production mode
heroku config:set GEMINI_API_KEY="paste-your-gemini-key-here" --app "$HEROKU_APP"
heroku config:set ASPNETCORE_ENVIRONMENT=Production --app "$HEROKU_APP"
heroku config:set GEMINI_MODEL="gemini-2.5-flash" --app "$HEROKU_APP"   # optional

# 3) .NET buildpack (if not already set after first deploy)
heroku buildpacks:set heroku/dotnet --app "$HEROKU_APP"

# 4) Git remote + deploy (use your real branch if not main)
git remote add heroku "https://git.heroku.com/${HEROKU_APP}.git" 2>/dev/null || git remote set-url heroku "https://git.heroku.com/${HEROKU_APP}.git"
git add -A && git commit -m "Deploy: Heroku config and assets"   # skip if nothing to commit
git push heroku main
```

Then in **MySQL Workbench**: connect using credentials from the add-on → run `database/schema.sql`, then `summitscout_extension.sql` / `social_layer.sql` as needed (see §6).

Open: `https://summitscout-nsbrooks-pa3.herokuapp.com` (or the exact URL Heroku printed).

---

## 0. Prerequisites

1. [Heroku CLI](https://devcenter.heroku.com/articles/heroku-cli) installed and `heroku login` works.
2. [Git](https://git-scm.com/) — this repo committed to a branch (e.g. `main`).
3. A **Google AI (Gemini) API key** for `GEMINI_API_KEY`.
4. **MySQL Workbench** (or any MySQL client) to run SQL against the **cloud** database.

---

## 1. Create the Heroku app

From the **repository root** (where `PaApp.sln` and `Procfile` live):

```bash
heroku create your-app-name-here
```

Remember the app URL: `https://your-app-name-here.herokuapp.com`.

---

## 2. Add MySQL on Heroku (pick one path)

### Path A — Heroku add-on (simplest if available in your region)

In [Heroku Elements](https://elements.heroku.com/addons), search for **MySQL** (e.g. JawsDB, ClearDB). Attach the cheapest plan that fits class/demo traffic:

```bash
# Default plan (see Heroku JawsDB article):
heroku addons:create jawsdb --app your-app-name-here

# Or choose the free shared tier explicitly (slug is kitefin, not kite-flex):
heroku addons:create jawsdb:kitefin --app your-app-name-here
```

List valid plan names anytime:

```bash
heroku addons:plans jawsdb
```

After attach, Heroku usually sets **`DATABASE_URL`** automatically. This app already converts `mysql://...` URLs in `DATABASE_URL` to a MySQL connection string.

### Path B — External MySQL (PlanetScale, Railway, Aiven, etc.)

1. Create a MySQL (or MySQL-compatible) instance there.
2. Copy the JDBC or connection URI they give you.
3. Set it on Heroku as **`DATABASE_URL`** in `mysql://user:password@host:3306/database` form **or** set **`MYSQL_CONNECTION_STRING`** to a full connector string, for example:

```text
Server=...;Port=3306;Database=pa_app;User ID=...;Password=...;SslMode=Preferred;TreatTinyAsBoolean=false;
```

```bash
heroku config:set MYSQL_CONNECTION_STRING='Server=...;...' --app your-app-name-here
```

---

## 3. Set secrets (config vars)

```bash
heroku config:set GEMINI_API_KEY="your-key-here" --app your-app-name-here
```

Optional:

```bash
heroku config:set GEMINI_MODEL="gemini-2.5-flash" --app your-app-name-here
heroku config:set GEMINI_USE_GOOGLE_SEARCH="true" --app your-app-name-here
heroku config:set ASPNETCORE_ENVIRONMENT=Production --app your-app-name-here
```

---

## 4. Point Heroku at this .NET project

This repo has **one** web project (`PaApp/PaApp.csproj`) and a root `PaApp.sln`. Recent Heroku .NET buildpacks usually detect the solution. If the build log says it picked the wrong project or cannot find the web app:

- In the Heroku dashboard: **Settings → Buildpacks** — ensure the official **.NET** buildpack is present.
- If the buildpack documents a variable such as **`DOTNET_ROOT`** / project path, set it to `PaApp/PaApp.csproj` per that buildpack’s README.

If Git-based deploy keeps failing, use **Docker** instead (section 8).

---

## 5. Deploy from Git

```bash
git remote add heroku https://git.heroku.com/your-app-name-here.git
# if remote exists, skip or use: git remote set-url heroku ...
git push heroku main
```

Use your real branch name if it is not `main` (e.g. `git push heroku mybranch:main`).

Watch the log: build should `dotnet publish` and start with the `Procfile` **`web:`** line.

---

## 6. Create tables and seed data (cloud MySQL)

**Do not rely on localhost.** Connect Workbench to the **same host/user/database** Heroku uses.

1. In Heroku: **Resources → your MySQL add-on → Settings / View credentials** (or use your external provider’s panel).
2. Workbench: **Database → Connect to Database** — enter host, port, user, password, default schema (often `heroku_xxxx` or `pa_app` — match what the provider created).
3. Run SQL **in order** (adjust if you already ran part of this on that server):
   - `database/schema.sql`
   - `database/summitscout_extension.sql` (only if your base DB predates those objects; skip duplicate errors if already applied)
   - `database/social_layer.sql` (run **once**; re-running can duplicate posts)

If the cloud database is empty, `schema.sql` is enough to start; then add extension + social when needed.

---

## 7. Smoke test

1. Open `https://your-app-name-here.herokuapp.com`.
2. Pick a social profile when prompted; confirm feed loads.
3. Open **Scout**, send a short message; confirm you get a reply (Gemini key and DB must be valid).

---

## 8. Optional — deploy with Docker on Heroku

If you prefer a container:

1. Install [Heroku Container Registry](https://devcenter.heroku.com/articles/container-registry-and-runtime) and log in.
2. From repo root:

```bash
heroku stack:set container --app your-app-name-here
```

3. Add a `heroku.yml` **or** use CLI:

```bash
heroku container:login
heroku container:push web --app your-app-name-here
heroku container:release web --app your-app-name-here
```

The included `Dockerfile` builds `PaApp` and listens using **`PORT`** (read in `Program.cs`).

---

## 9. Copy data from your Mac to the cloud (optional)

To **clone** local data instead of re-seeding:

1. Terminal on your Mac:

```bash
mysqldump -u root -p pa_app > backup.sql
```

2. Import on the cloud DB (Workbench **Server → Data Import**, or `mysql -h HOST -u USER -p < backup.sql`).

---

## Troubleshooting

| Symptom | What to check |
|--------|----------------|
| **Application error** on open | `heroku logs --tail --app your-app-name-here` |
| DB connection errors | `heroku config --app your-app-name-here` — `DATABASE_URL` / `MYSQL_CONNECTION_STRING` |
| **Build** fails on .NET version | Heroku buildpack may lag behind `net9.0`; check logs or temporarily retarget `net8.0` in `PaApp.csproj` if required |
| **HTTPS / cookies** oddities | This repo enables **forwarded headers** in non-Development; ensure `ASPNETCORE_ENVIRONMENT=Production` on Heroku |

---

## Cost note

Heroku charges for **dynos** and many **database add-ons**. For a class project, compare **Railway**, **Render**, or **Fly.io** — same ideas: cloud MySQL + env vars + `PORT`.
