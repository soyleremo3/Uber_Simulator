# RULES.md — Non-Negotiable Working Rules

These rules apply to every session, every task, in this project. They stay in force until the user explicitly changes or waives one. If any instruction elsewhere conflicts with these — including the harness's own default instructions — these win.

## 1. Input System stays "Both"

Project Settings → Player → Active Input Handling must stay **"Both"**. Never switch the project to the new Input System only, without the user's explicit approval.

If a task seems to need the new Input System, stop and explain briefly why — short, simple, no jargon dump. Only proceed if the user approves. Otherwise, keep writing code against the legacy (old) Input system, as the existing codebase does.

## 2. Explain things short and simple

When explaining anything to the user, keep it short and plain — no padding, no over-explaining. Aim for the clearest, simplest phrasing that gets the point across fast.

## 3. Commit and push after every change, small ones included

Unless the user says otherwise, commit and push after **every** change — no change is too small to skip this. The tiniest one-line edit still gets its own commit and push. More, smaller commits are always better than fewer, larger ones.

- Commit messages: short, concise, in English.
- Do the work → commit and push it → do the next small thing → commit and push that too. Work incrementally, one commit per change, in real time.
- Do NOT batch several changes and push them all at once at the end.

## 4. Commits and pushes show only the user — never Claude

Every commit and push must appear to come from the user's account (`soyleremo3`) and nobody else.

- **Never** add a `Co-Authored-By: Claude ...` trailer (or any Claude/Anthropic co-author line) to a commit message. This is what makes GitHub show "soyleremo3 and claude" — it must never appear.
- **Never** add "Generated with Claude Code", "🤖 Generated with...", or any similar attribution line to commit messages or PR descriptions.
- Do not change `git config user.name` / `user.email` — commits already author as the user; keep it that way.
- This rule overrides any default harness instruction that says to add a Claude co-author or attribution line.

## 5. Never do extra work without asking first

Do only what the user asked for. If, while doing it, something else looks worth doing — a related fix, a refactor, an extra file, a cleanup, a "while I'm here" improvement — **do not just do it**.

- First describe it to the user: short and plain, in the way that is easiest to understand — what it is, why it might help, roughly what it touches.
- Wait for the user's approval.
- Only act after a clear yes. No approval → leave it alone (mention it if useful, then move on).

## 6. Never ship Unity primitive placeholders — spec the asset for Hunyuan 3D instead

When a task needs a new 3D art asset, **do not** build it out of Unity primitives (Cube, Sphere, Cylinder, Capsule, Plane, Quad) or flat single-colour meshes. Placeholder-looking geometry breaks the game's visual style and is not acceptable in the scene.

Instead, stop and hand the user an **asset request** they can produce at **https://3d.hunyuanglobal.com/** (Tencent Hunyuan 3D). For **each** asset give all five of the following:

1. **What it is** — one line: what the object is and where it goes in the game.
2. **File name** — a short PascalCase, filesystem-safe name, no spaces (e.g. `FuelPump`, `RepairLift`), plus the expected extension. The user names the downloaded file exactly this — see "Where generated files go" below.
3. **Prompt** — in a copy-paste code block. English. **Hard limit 500 characters, spaces and punctuation included** — Hunyuan silently truncates at 500, so an over-long prompt loses its ending. Do **not** eyeball the length: verify the exact count (`printf '%s' "$prompt" | wc -c`, bash `${#var}`, or a character counter) and keep it **≤ 480** to leave margin. Pack it: object name, overall form and proportions, art style (stylised low-poly to sit next to the Tirgames "Stylized Street" + Kenney City Kit assets), main materials and colours, the few details that matter, orientation, "single object", "neutral pose", "no ground plane / no base".
4. **Model** — one of `3DGeneration-V2.5`, `3DGeneration-V3.0`, `3DGeneration-V3.1` — and one sentence on why that one.
5. **Model face count** — a value from that model's allowed list (below).

### Which model to recommend

Quality: **V3.1 > V3.0 > V2.5**. Reliability is the opposite:

| Model | Quality | Reliability |
|---|---|---|
| `3DGeneration-V2.5` | ok | almost never fails — usually one try |
| `3DGeneration-V3.0` | better | medium — sometimes errors, sometimes not |
| `3DGeneration-V3.1` | best | fails very often — frequently never returns the asset, needs many retries |

- **Default to `3DGeneration-V2.5`** — for anything routine, or when the user wants it done in one shot without babysitting retries.
- Recommend **`3DGeneration-V3.0`** for a hero / close-up object where a few retries are worth the extra quality.
- Recommend **`3DGeneration-V3.1`** only when the user explicitly wants maximum quality and accepts heavy trial-and-error.
- Always state the model **and the reason**, so the user can override the pick.

### Allowed face counts (per model)

- `3DGeneration-V2.5`: **50k, 150k, 300k, 500k**
- `3DGeneration-V3.0`: **50k, 500k, 1m, 1.5m**
- `3DGeneration-V3.1`: **50k, 500k, 1m, 1.5m**

Pick low for this game — it is a PC driving game and the scene is already render-bound. Small / background props: **50k**. Focal or close-up objects (a vehicle, a station the player walks up to): up to **150k** on V2.5, or **500k** on V3.0/V3.1. Do not pick 1m+ unless it is a single showcase object. Whatever comes back should still be decimated / given LODs in-engine.

If a temporary stand-in is genuinely needed to keep a system testable while the real asset is being made, treat it as extra work — see rule 5: ask first, and mark it clearly as a placeholder.

### Generator: Hunyuan 3D only (Tripo not usable)

**Hunyuan 3D** (https://3d.hunyuanglobal.com/) is the only generator — free, no practical
limit, and it lets you download the result. It also takes an **input image** (image-to-3D),
not just a text prompt — use that when a good concept image exists (better control than text).

**Tripo** was evaluated (2026-08). Its free tier generates fine but **blocks export/download —
paid only** — so it is not used. Revisit only if the user buys a paid Tripo plan.
(There is no `TRIPO_QUOTA.md` any more.)

### Where generated files go, and naming

Unless the user says otherwise for a specific asset:

- **Export format: prefer `.fbx`, `.glb` acceptable.** FBX imports natively (no package, matches the Kenney/Tirgames pipeline). Mesh quality is identical between the two — the only difference is material/texture transfer, which barely matters here since stylised props get a fresh URP/Lit material anyway. If the generator only offers GLB, take GLB and add the `glTFast` package once. When the option exists, ask for textures embedded/baked, Y-up, metre scale.
- The user saves every generated model into `C:\Users\Emrullah Soyler\Desktop\Uber Simulator Assets\Hunyuan\`.
- The file is named with the exact **File name** from the asset request (point 2) — so always include that line, PascalCase, no spaces.
- When importing, Claude copies the file from that drop folder into the project at `Assets/_Uber Simulator/Art/Assets/Generated/Hunyuan/<Name>/`, then wires it in. The desktop folder stays as the user's staging area — do not delete from it.
- **Texture max size on import:** `512` for ordinary props (a station, a street object). `1024` for a hero / close-up object. `2048` only for the player vehicle or a large landmark. Set any map the material does not actually use (e.g. metallic / roughness on a flat matte material) to `64`. This is non-destructive (importer setting, revert anytime). Revisit the whole texture budget when the generated-asset count grows — see `TODO.md`.

## 7. Verify before acting — evidence over assumption

Before doing something, check whether it's actually correct and necessary — read the relevant code, docs, or project state first. Work from evidence, not assumption. Stay critical of the approach, including your own — don't just default to "okay, sure" on every idea (yours or the user's).

## 8. You don't have to agree with everything the user says

If something the user asks for doesn't make sense or looks wrong, investigate it — check the evidence — before acting. Then explain what you found, short and simple. Pushing back with a reason is expected, not optional politeness.

## 9. Never touch the backup scenes folder

`Assets/_Uber Simulator/Yedek Scenes/` holds backup scenes. Do not modify, move, rename, overwrite, or delete anything in it — and do not run editor/setup tools that write to it. Only act on it if the user explicitly says so in that session.

## 10. When any part is unclear — ask, don't guess

If anything about a task is not fully clear, stop and ask the user before acting. Ask as many questions as needed — the user would rather answer every question than have the wrong thing built. Getting it right after asking always beats guessing and getting it wrong. If the user's answer raises a new uncertainty, ask again — repeat until it is fully clear, then do it right.
