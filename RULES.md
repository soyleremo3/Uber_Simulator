# RULES.md — Non-Negotiable Working Rules

These rules apply to every session, every task, in this project. They stay in force until the user explicitly changes or waives one. If any instruction elsewhere conflicts with these, these win.

## 1. Input System stays "Both"

Project Settings → Player → Active Input Handling must stay **"Both"**. Never switch the project to the new Input System only, without the user's explicit approval.

If a task seems to need the new Input System, stop and explain briefly why — short, simple, no jargon dump. Only proceed if the user approves. Otherwise, keep writing code against the legacy (old) Input system, as the existing codebase does.

## 2. Explain things short and simple

When explaining anything to the user, keep it short and plain — no padding, no over-explaining. Aim for the clearest, simplest phrasing that gets the point across fast.

## 3. Commit and push after every change, small ones included

Unless the user says otherwise, commit and push after every change — no change is too small to skip this. More, smaller commits are better than fewer, larger ones.

- Commit messages: short, concise, in English.
- Do the work → commit and push it → do the next small thing → commit and push that too. Work incrementally, one commit per change, in real time.
- Do NOT batch everything and push it all at once at the end.

## 4. Verify before acting — evidence over assumption

Before doing something, check whether it's actually correct and necessary — read the relevant code, docs, or project state first. Work from evidence, not assumption. Stay critical of the approach, including your own — don't just default to "okay, sure" on every idea (yours or the user's).

## 5. You don't have to agree with everything the user says

If something the user asks for doesn't make sense or looks wrong, investigate it — check the evidence — before acting. Then explain what you found, short and simple. Pushing back with a reason is expected, not optional politeness.

## 6. Never touch the backup scenes folder

`Assets/_Uber Simulator/Yedek Scenes/` holds backup scenes. Do not modify, move, rename, overwrite, or delete anything in it — and do not run editor/setup tools that write to it. Only act on it if the user explicitly says so in that session.
