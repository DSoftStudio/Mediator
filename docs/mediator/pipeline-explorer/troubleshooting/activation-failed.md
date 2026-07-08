---
layout: default
title: "License activation failed - Pipeline Explorer"
description: "Loading your .dslic license file didn't activate Pipeline Explorer. Causes and resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Troubleshooting](index.md)

# License activation failed

## Symptom

You loaded your `.dslic` license file — via **Mediator: License Status** in VS Code, or the activation screen in Visual Studio — but the device didn't activate. The commercial features stayed locked, or you saw a message that the license was rejected, the seat limit was reached, or the file wasn't a valid license.

Work through the causes below in order.

---

## 1. The file isn't a valid `.dslic` license

Activation expects the `.dslic` envelope DSoftStudio sends you after you subscribe (or start a trial). It rejects anything else — a plain token saved into a file, a renamed `.txt`, or a partial download.

**Check**

- The file you selected has the `.dslic` extension and is the one you received from DSoftStudio.
- It downloaded completely (re-download it from the [customer portal](https://portal.dsoftstudio.com/login) if you're unsure).

**Fix**

Load the original `.dslic` file. If you only have its contents in an email, save them to a file named `license.dslic` and load that. If activation still reports the file isn't a valid license, re-download it from the customer portal.

---

## 2. There's no subscription to activate yet

If you haven't subscribed, there is nothing to load. During the launch window the commercial features unlock automatically under the free access period; after that, a subscription is required.

**Check**

The status shows a trial / free-access countdown, or reports that no license was found.

**Fix**

- If you're still inside the free access period, the commercial features are already unlocked — no `.dslic` file is needed.
- To subscribe — or to start the optional 14-day paid-subscription trial — open the [Pricing page](https://mediator.dsoftstudio.com/pricing). Checkout and trials are handled by our payment provider, Paddle; see the [Terms of Service](https://mediator.dsoftstudio.com/terms) for trial and billing details. After checkout you receive your `.dslic` file by email.

---

## 3. Seat limit reached

Each plan allows a fixed number of activated machines — one for Individual, more for Teams and Enterprise plans. Activating on more machines than your plan allows is rejected.

**Check**

The message indicates the seat limit for your subscription has been reached.

**Fix**

- Free up a seat: open the [customer portal](https://portal.dsoftstudio.com/login), find a machine you no longer use, and release it — then activate again.
- Moving this license from another machine? Open the [customer portal](https://portal.dsoftstudio.com/login), open the license, and **Deactivate** the old machine — then activate again on the new one.
- Or upgrade to a plan with more seats on the [Pricing page](https://mediator.dsoftstudio.com/pricing).

---

## 4. Activated on different hardware

A seat is bound to the machine it was activated on. If you reimaged the machine, swapped major hardware, or moved the drive to a new computer, the binding no longer matches and activation is refused.

**Check**

The message mentions a machine or hardware mismatch.

**Fix**

Open the [customer portal](https://portal.dsoftstudio.com/login), open the license, and **Deactivate** the old machine — then activate again on the new machine.

---

## 5. Can't reach the activation service

Activation may need a one-time online check. On a network that blocks outbound HTTPS — a corporate proxy or firewall — that check can't complete. Once the device is activated, the extension keeps working offline.

**Check**

Confirm the machine has general internet access.

**Fix**

- If you are behind a corporate proxy, configure your IDE's proxy:
  - **VS Code** — the `http.proxy` setting.
  - **Visual Studio** — inherits the Windows proxy settings.
- If outbound HTTPS is blocked, ask your IT team to allow access to DSoftStudio's services.
- Retry activation once connectivity is restored.

---

## 6. The system clock is wrong

Activation is time-sensitive. If the system clock is significantly off, activation can be rejected.

**Check & fix**

Enable automatic date & time, let the clock sync, then retry:

- **Windows** — Settings → Time & language → Date & time → **Sync now**.
- **macOS** — System Settings → General → Date & Time → **Set automatically**.
- **Linux** — enable NTP: `sudo timedatectl set-ntp true`.

---

## Still stuck?

Email **licensing@dsoftstudio.com** with:

- The exact message shown on the activation screen
- Your IDE and extension version
- Your subscription plan or order email, so we can match your seats

See the [Terms of Service](https://mediator.dsoftstudio.com/terms) for seat, trial, and transfer policies.

---

[← Back to Troubleshooting](index.md)
