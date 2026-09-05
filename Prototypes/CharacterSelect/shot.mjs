import { chromium } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const distPath = path.join(__dirname, "dist", "index.html");
const fileUrl = "file://" + distPath;

const viewports = [
  { w: 844, h: 390 },
  { w: 932, h: 430 },
  { w: 1180, h: 820 },
];

const CHROMIUM_PATH = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome";

async function main() {
  const browser = await chromium.launch({
    executablePath: CHROMIUM_PATH,
    headless: true,
  });

  let anyErrors = false;

  for (const vp of viewports) {
    const context = await browser.newContext({
      viewport: { width: vp.w, height: vp.h },
      deviceScaleFactor: 2,
    });
    const page = await context.newPage();

    const consoleErrors = [];
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        consoleErrors.push(msg.text());
      }
    });
    page.on("pageerror", (err) => {
      consoleErrors.push("pageerror: " + err.message);
    });

    await page.goto(fileUrl, { waitUntil: "load" });
    await page.waitForTimeout(1500);

    const shot1 = path.join(__dirname, "shots", `select-${vp.w}x${vp.h}.png`);
    await page.screenshot({ path: shot1 });

    // Click the 3rd carousel icon (index 2). Icons are motion.button inside the carousel drag container.
    const icons = await page.locator("button").all();
    // Find carousel icon buttons: they are the round buttons with width ICON(64)*something.
    // Simplify: query all buttons, filter by bounding box size close to 64 (unselected) - fallback to nth in carousel container.
    const carouselButtons = page.locator("div[style*='touch-action']").locator("button");
    const count = await carouselButtons.count();
    if (count >= 3) {
      await carouselButtons.nth(2).click({ force: true });
    } else if (icons.length >= 3) {
      await icons[icons.length - 3]?.click({ force: true }).catch(() => {});
    }

    await page.waitForTimeout(900);

    const shot2 = path.join(__dirname, "shots", `select-${vp.w}x${vp.h}-3rd.png`);
    await page.screenshot({ path: shot2 });

    if (consoleErrors.length > 0) {
      anyErrors = true;
      console.log(`\n[${vp.w}x${vp.h}] Console errors:`);
      for (const e of consoleErrors) console.log("  " + e);
    } else {
      console.log(`[${vp.w}x${vp.h}] No console errors.`);
    }

    await context.close();
  }

  await browser.close();

  if (anyErrors) {
    console.log("\nRESULT: console errors were found.");
    process.exitCode = 1;
  } else {
    console.log("\nRESULT: no console errors across all viewports.");
  }
}

main().catch((err) => {
  console.error("shot.mjs failed:", err);
  process.exitCode = 1;
});
