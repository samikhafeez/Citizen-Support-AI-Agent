#!/usr/bin/env python3
"""
diagnose_bin.py — Standalone Playwright diagnostic for Bradford Council bin collection.

Run this INSIDE the councilchatbot Docker container to see exactly what Bradford's
website outputs at every step. This is read-only; it does not change any app state.

Usage (on EC2):
    docker exec -it councilchatbot python3 /app/test/diagnose_bin.py BD3 8PX

Or with a specific address:
    docker exec -it councilchatbot python3 /app/test/diagnose_bin.py BD3 8PX "12 SOME ROAD, BRADFORD"
"""

import sys
import asyncio
from playwright.async_api import async_playwright

TARGET_URL = "https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb"

async def run(postcode: str, address_fragment: str | None = None):
    print(f"\n{'='*60}")
    print(f"  Bradford Bin Collection Diagnostics")
    print(f"  Postcode: {postcode}")
    print(f"{'='*60}\n")

    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        page    = await browser.new_page()
        page.set_default_timeout(60_000)

        # ── Step 1: Load the form page ────────────────────────────────────────
        print(f"[1] Navigating to {TARGET_URL} ...")
        await page.goto(TARGET_URL, wait_until="domcontentloaded")
        await page.wait_for_load_state("networkidle")

        body = await page.locator("body").inner_text()
        print("\n--- PAGE BODY (initial) ---")
        for line in body.splitlines():
            if line.strip():
                print(f"  {line.strip()}")

        # ── Step 2: Fill postcode and submit ─────────────────────────────────
        print(f"\n[2] Filling postcode '{postcode}' ...")
        inputs = page.locator("input[type='text']")
        count  = await inputs.count()
        print(f"    Found {count} text input(s)")

        if count == 0:
            print("    ERROR: No text input found — page may have changed structure")
            await browser.close()
            return

        await inputs.first.fill(postcode)
        await inputs.first.press("Tab")

        # Find the submit button
        buttons = page.locator("button, input[type='submit'], input[type='button']")
        btn_count = await buttons.count()
        print(f"    Found {btn_count} button(s):")
        for i in range(btn_count):
            btn  = buttons.nth(i)
            text = (await btn.inner_text() if await btn.count() else "").strip()
            val  = await btn.get_attribute("value") or ""
            print(f"      [{i}] text={repr(text)}  value={repr(val)}")

        # Click the first button that looks like "Find address"
        clicked = False
        for i in range(btn_count):
            btn  = buttons.nth(i)
            text = ((await btn.inner_text()) if await btn.count() else "").strip().lower()
            val  = (await btn.get_attribute("value") or "").lower()
            if "find" in text or "find" in val or "search" in text:
                print(f"    Clicking button [{i}]: {repr(text or val)}")
                await btn.click()
                clicked = True
                break

        if not clicked:
            print("    WARNING: Could not identify Find Address button — clicking first submit")
            await buttons.first.click()

        await page.wait_for_load_state("networkidle")
        await page.wait_for_timeout(3000)

        # ── Step 3: Show all address buttons ─────────────────────────────────
        print("\n[3] Address buttons found after postcode submit:")
        all_btns = page.locator("button, input[type='button'], input[type='submit'], a")
        all_count = await all_btns.count()
        address_buttons = []
        for i in range(all_count):
            btn  = all_btns.nth(i)
            text = ""
            try:
                text = (await btn.inner_text()).strip()
            except:
                text = (await btn.get_attribute("value") or "").strip()
            if text and ("BD" in text.upper() or "ROAD" in text.upper() or
                         "STREET" in text.upper() or "LANE" in text.upper() or
                         "BRADFORD" in text.upper() or "AVENUE" in text.upper()):
                address_buttons.append((i, text))
                print(f"  ADDRESS [{i}]: {repr(text)}")

        if not address_buttons:
            body2 = await page.locator("body").inner_text()
            print("\n  No address buttons found. Full page body:")
            for line in body2.splitlines():
                if line.strip():
                    print(f"    {line.strip()}")
            await browser.close()
            return

        # ── Step 4: Click an address ──────────────────────────────────────────
        target_idx, target_text = address_buttons[0]
        if address_fragment:
            for idx, txt in address_buttons:
                if address_fragment.lower() in txt.lower():
                    target_idx, target_text = idx, txt
                    break

        print(f"\n[4] Clicking address [{target_idx}]: {repr(target_text)}")
        await all_btns.nth(target_idx).click()
        await page.wait_for_load_state("networkidle")
        await page.wait_for_timeout(3000)

        body3 = await page.locator("body").inner_text()
        print("\n--- PAGE BODY (after address click) ---")
        for line in body3.splitlines():
            if line.strip():
                print(f"  {line.strip()}")

        # ── Step 5: Find and click "Show collection dates" ────────────────────
        print("\n[5] All interactive elements after address click:")
        interactive = page.locator("button, input[type='button'], input[type='submit'], a[href], [role='button']")
        ic = await interactive.count()
        show_idx = None
        for i in range(ic):
            el   = interactive.nth(i)
            text = ""
            try:
                text = (await el.inner_text()).strip()
            except:
                text = (await el.get_attribute("value") or "").strip()
            if text:
                print(f"  [{i}] {repr(text)}")
                tl = text.lower()
                if any(w in tl for w in ["show", "view", "collection", "date", "continue"]):
                    if show_idx is None:
                        show_idx = i
                        print(f"       ^^^^ CANDIDATE for 'Show collection dates'")

        if show_idx is None:
            print("\n  ERROR: No candidate for 'Show collection dates' found")
            await browser.close()
            return

        print(f"\n[6] Clicking element [{show_idx}] ...")
        await interactive.nth(show_idx).click()
        await page.wait_for_load_state("networkidle")
        await page.wait_for_timeout(3000)

        # ── Step 6: Dump the final results page ───────────────────────────────
        body4 = await page.locator("body").inner_text()
        print("\n--- FINAL PAGE BODY (after Show collection dates) ---")
        lines = [l.strip() for l in body4.splitlines() if l.strip()]
        for i, line in enumerate(lines):
            print(f"  [{i:03d}] {line}")

        print(f"\n{'='*60}")
        print("  Diagnostics complete. Look for date lines above.")
        print(f"  Total lines on final page: {len(lines)}")
        print(f"{'='*60}\n")

        await browser.close()


if __name__ == "__main__":
    postcode = sys.argv[1] if len(sys.argv) > 1 else "BD3 8PX"
    address  = sys.argv[2] if len(sys.argv) > 2 else None
    asyncio.run(run(postcode, address))
