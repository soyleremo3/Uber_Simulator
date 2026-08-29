# TRIPO_QUOTA.md — Tripo free-account model budget

The project uses a **free** Tripo account: about **13 models per month**
(≈200 credits, ~7–40 credits per model), resetting on the **1st of each
calendar month**. Tracked by hand here — the Tripo web and API budgets are
separate and there is no reliable programmatic counter.

## Current

- **Month:** 2026-08
- **Models left this month:** 13 / 13
- **Resets:** 1st of each month, back to 13

## Rules for keeping this file correct

1. **Before** generating on Tripo: check *Models left this month* > 0.
   If it is a new month, first change *Month* to the current month and set
   *Models left this month* back to 13.
2. **After** each Tripo generation: subtract 1, write the new number, add a
   row to the log, and commit the file.
3. **Out-of-band changes** (bought credits, plan upgrade, a failed generation
   that got refunded, etc.): the user tells Claude the new number; update it
   here and note it in the log.

## Tool split (full policy: RULES.md rule 6)

- **Hunyuan 3D** — simple / background / low-detail assets, quality not
  critical. Default. No practical limit.
- **Tripo** — only assets that must genuinely look good (hero props, close-up
  gameplay objects). Spend the 13/month budget only on these.

## Log

| Date | Asset | Tool | Tripo left after |
|---|---|---|---|
| 2026-08-29 | budget initialised | — | 13 |
