---
layout: default
title: "License activation failed - Pipeline Explorer"
description: "Pipeline Explorer rejected your activation token. Causes and resolutions."
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

You pasted your activation token in the License Status panel (VS Code) or activation flyout (Visual Studio) and saw one of the following:

- `Activation failed: invalid token`
- `Activation failed: could not reach license server`
- `Activation failed: seat limit reached`
- `Activation failed: hardware fingerprint mismatch`
- `Trial expired — please activate a license`

Each cause has a specific resolution.

---

## 1. Invalid token (typo or paste truncation)

By far the most common cause. Tokens are long opaque strings; pasting from email or chat clients can silently strip whitespace or truncate the value.

**Check**

Compare the token in your settings against the one in the [pricing portal](https://mediator.dsoftstudio.com/pricing) account page. The token starts with a fixed prefix and ends with a checksum segment — both must be present.

**Fix**

1. Copy the token again from the portal, using the **Copy** button next to it (not a manual select + copy).
2. Paste it into the activation flyout.
3. Click **Activate** — do not press **Enter** mid-paste, which some IDEs interpret as a submit on a partial value.

If the token still fails immediately (within a second of clicking **Activate**), it is almost certainly malformed. Re-copy from the portal.

---

## 2. Could not reach license server

The activation flow requires a one-time round trip to the license server. Subsequent IDE launches work offline using a cached proof.

**Check**

From the same machine:

```shell
curl -I https://license.dsoftstudio.com/api/v1/activate
```

A response of `HTTP/2 401` or `HTTP/2 405` is expected (the endpoint rejects an unauthenticated GET but proves the host is reachable). A timeout or `connection refused` means the network is blocked.

**Fix**

- Verify your machine has internet access.
- If you are behind a corporate proxy, configure your IDE's HTTP proxy:
  - **VS Code** — `http.proxy` setting.
  - **Visual Studio** — proxy is inherited from Windows / Internet Explorer settings; see [Microsoft's proxy configuration guide](https://learn.microsoft.com/en-us/visualstudio/install/install-visual-studio-behind-a-firewall-or-proxy-server).
- If your firewall blocks outbound HTTPS, ask your IT team to whitelist `license.dsoftstudio.com` on port 443.

After fixing connectivity, click **Activate** again.

---

## 3. Hardware fingerprint mismatch

Each activation binds the token to a hardware fingerprint derived from your machine. If you reimaged, moved drives, joined a new domain, or replaced the motherboard, the fingerprint changes and the cached proof becomes invalid.

**Check**

The error message includes the literal text `hardware fingerprint mismatch` or `machine mismatch`.

**Fix**

You need a **hardware migration release** — a one-click operation in the customer portal that releases the previous binding so the same token can re-activate on the new fingerprint.

1. Open <https://portal.dsoftstudio.com/login> and sign in.
2. Navigate to **Subscriptions → Active seats**.
3. Find the seat bound to your old machine and click **Release**.
4. Return to your IDE and click **Activate** again.

The release window is rate-limited (one release per 30 days per seat by default) to deter casual sharing. If you need an emergency release outside the window, contact licensing@dsoftstudio.com with your token ID.

---

## 4. Seat limit reached

You purchased a single-seat or team-seat license and tried to activate on more machines than your plan allows.

**Check**

The error message reads `seat limit reached (n/n)` where `n` is your plan's seat count.

**Fix**

Three options:

- **Release an unused seat** in the portal as described above.
- **Upgrade your plan** to a higher seat count at <https://mediator.dsoftstudio.com/pricing>.
- **Sign in to the portal** to see all active seats — if any of them are old / no longer in use, release them first.

---

## 5. Trial expired

The free trial ran out of time and no production token has been activated.

**Check**

The status panel reads `Trial expired — please activate a license`.

**Fix**

- Purchase a license at <https://mediator.dsoftstudio.com/pricing>.
- Paste the production token in the activation flyout.

If you believe your trial should still be active and the IDE is showing this error in error, contact licensing@dsoftstudio.com — trials can be extended on a case-by-case basis for serious evaluation.

---

## 6. Clock skew on the machine

The activation protocol uses signed time-bound proofs. If your system clock is more than five minutes off from the license server, activation is rejected as a replay-protection measure.

**Check**

On Windows:

```powershell
w32tm /query /status
```

On macOS / Linux:

```shell
date -u
```

Compare against [time.is](https://time.is/) or any NTP source.

**Fix**

- **Windows** — `Settings → Time & language → Sync now`.
- **macOS** — `System Settings → General → Date & Time → Set automatically`.
- **Linux** — enable `timesyncd` or `chrony`:

  ```shell
  sudo timedatectl set-ntp true
  ```

After the clock is back in sync (within a minute or two), retry activation.

---

## Still stuck?

Email **licensing@dsoftstudio.com** with:

- Your activation token (the last 8 characters only, for matching)
- The exact error message displayed
- Your IDE and version
- Output of `curl -I https://license.dsoftstudio.com/api/v1/activate`

Most activation issues are resolved within one business day.

---

[← Back to Troubleshooting](index.md)
