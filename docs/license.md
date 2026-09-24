# Activating the Unity Personal license

Unity Personal is free (the revenue limit is in Unity's terms), but the editor
won't open without an activated license. You do this once, and the license is
kept in the `home` volume.

## Before you start

- A **Unity ID** (https://id.unity.com). Create it in your normal browser and
  confirm the e-mail before you begin. Doing that inside the container works
  too, but it's slower.
- The image is built (`docker compose build`).

## 1. Start the desktop

```sh
docker compose up -d
```

On the same machine, open <http://localhost:6080/vnc.html> and click **Connect**.
From another machine, open a tunnel first and use the same address:

```sh
ssh -L 6080:localhost:6080 <docker-host>
```

## 2. Sign in to Unity Hub

1. Right-click the desktop → **Unity Hub (license)**. The first start takes a few seconds.
2. The Hub first shows the **Unity Terms of Service**. Read them, scroll to the
   bottom (the **Agree** button stays disabled until you do) and agree.
3. Close any "install an editor" prompt: the editor is already in the image.
4. Click **Sign in**. The Hub opens **Firefox** inside the desktop with Unity's login page.
5. Sign in. At the end, Firefox asks whether to open the `unityhub` link. Choose
   **Open link** (you can tick "always allow").
6. The Hub now shows your account.

**If the Hub doesn't react after the login**, the callback link didn't reach it:
- In Firefox, copy the `unityhub://…` address the page tried to open. If it
  doesn't show, look for it under ≡ → *Downloads* or in the page's "open the
  Hub" link.
- In a terminal (right-click → Terminal), run:
  `unityhub-gui 'unityhub://…the copied link…'`

## 3. Add the Personal license

1. In the Hub: gear icon (**Preferences**) → **Licenses** → **Add**.
2. Choose **Get a free personal license** and accept the terms.
3. The list now shows *Unity Personal*, active.
4. You can close the Hub. You only need it again to renew or reactivate.

## 4. Check it

In the terminal:

```sh
moddev-license
```

It should list the activated license. Then open the editor:

```sh
unity-gui
```

The editor should get past the license check and show its project dialog.
Close it, and you're done.

## If the editor shows "Install Unity Hub"

That window means the editor found **no valid license**. Unity 2019.4.18f1 has
no activation screen of its own: it relies on the Licensing Client (the same one
Hub 3 uses). Go back to step 3, and check `moddev-license`. The editor log is in
`~/unity-editor.log`; search it for `Licens`.

## Do the activation yourself

Accepting the Terms of Service and activating the license are steps for you, the
account holder, to do by hand. Unity's Terms (section 17.2, updated June 2026)
restrict how AI agents and automated clients may interact with Unity's
platform, so don't script this part or hand it to an assistant.

## Good to know

- The license is bound to the "machine". `compose.yml` pins the container's
  hostname and MAC address, and the image has a fixed `/etc/machine-id`, so
  recreating the container (`down`/`up`) keeps it valid. **Rebuilding the image**
  creates a new machine id and may require activating again, which is only
  step 3 (the Hub remembers your login).
- `docker compose down -v` deletes the volumes, license included.
