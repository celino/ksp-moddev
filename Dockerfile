# ksp-moddev: an isolated environment for developing Kerbal Space Program 1.12 mods
#
# Unity 2019.4.18f1 (the exact version KSP 1.12 runs on) comes from the GameCI
# image, with the Windows Mono module so asset bundles can target Windows (what
# most mods ship). On top of it: Unity Hub (only to activate the free Personal
# license), Firefox (the Hub signs in through a browser), a virtual desktop
# reachable from a web browser (Xvfb + openbox + x11vnc + noVNC), and the .NET
# SDK to build mod DLLs.
#
# Nothing from Squad/Take-Two is baked into the image: PartTools is downloaded at
# runtime (bin/fetch-parttools) and the game's DLLs are mounted as a volume (/ksp).

ARG UNITY_IMAGE=unityci/editor:ubuntu-2019.4.18f1-windows-mono-3.2.2
FROM ${UNITY_IMAGE}

ARG DEBIAN_FRONTEND=noninteractive
ARG USER_NAME=modder
ARG USER_UID=1000
ARG USER_GID=1000
ARG DOTNET_CHANNEL=8.0

# Virtual desktop + software GL (llvmpipe), so the editor runs without a GPU.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl gnupg git unzip xz-utils sudo less nano \
      xvfb x11vnc openbox xterm novnc websockify dbus-x11 xdg-utils x11-apps \
      libgl1-mesa-dri libglu1-mesa mesa-utils \
      fonts-dejavu-core \
      libgbm1 libasound2 libnss3 libsecret-1-0 libxss1 libgtk-3-0 \
 && rm -rf /var/lib/apt/lists/*

# Firefox from Mozilla's APT repo: Ubuntu's own package is a snap stub that
# doesn't work in containers. The Hub opens it for the Unity ID sign-in.
RUN curl -fsSL https://packages.mozilla.org/apt/repo-signing-key.gpg \
      -o /usr/share/keyrings/packages.mozilla.org.asc \
 && echo "deb [signed-by=/usr/share/keyrings/packages.mozilla.org.asc] https://packages.mozilla.org/apt mozilla main" \
      > /etc/apt/sources.list.d/mozilla.list \
 && printf 'Package: *\nPin: origin packages.mozilla.org\nPin-Priority: 1000\n' \
      > /etc/apt/preferences.d/mozilla \
 && apt-get update \
 && apt-get install -y --no-install-recommends firefox \
 && rm -rf /var/lib/apt/lists/*

# Unity Hub: only used to activate the Personal license (it writes
# Unity_lic.ulf into the home directory, which lives in a volume).
RUN curl -fsSL https://hub.unity3d.com/linux/keys/public \
      | gpg --dearmor -o /usr/share/keyrings/unityhub.gpg \
 && echo "deb [signed-by=/usr/share/keyrings/unityhub.gpg] https://hub.unity3d.com/linux/repos/deb stable main" \
      > /etc/apt/sources.list.d/unityhub.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends unityhub \
 && rm -rf /var/lib/apt/lists/*

# The sign-in comes back to the Hub through a unityhub:// link. Route it (and
# plain web links) through our wrappers so the Hub always gets --no-sandbox.
# The Hub registers its own unityhub.desktop as the handler in the user's
# mimeapps.list on every start, so that entry is rewritten to use the wrapper too.
COPY desktop/ /usr/share/applications/
RUN sed -i 's|^Exec=.*|Exec=unityhub-gui %U|' /usr/share/applications/unityhub.desktop \
 && printf '[Default Applications]\nx-scheme-handler/unityhub=unityhub-moddev.desktop\nx-scheme-handler/http=firefox.desktop\nx-scheme-handler/https=firefox.desktop\ntext/html=firefox.desktop\n' \
      > /etc/xdg/mimeapps.list \
 && update-desktop-database /usr/share/applications 2>/dev/null || true

# .NET SDK for mod DLLs (SDK-style projects targeting net48 with
# Microsoft.NETFramework.ReferenceAssemblies build fine on Linux).
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel ${DOTNET_CHANNEL} --install-dir /opt/dotnet \
 && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
 && rm /tmp/dotnet-install.sh
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# A user with the same UID/GID as the owner of the mounted repos, so files
# created in the volumes keep the right ownership. Drops any user that already
# holds that UID in the base image.
RUN existing="$(getent passwd ${USER_UID} | cut -d: -f1)"; \
    if [ -n "$existing" ] && [ "$existing" != "${USER_NAME}" ]; then userdel -r "$existing" || true; fi; \
    getent group ${USER_GID} >/dev/null || groupadd -g ${USER_GID} ${USER_NAME}; \
    useradd -m -u ${USER_UID} -g ${USER_GID} -s /bin/bash ${USER_NAME} \
 && echo "${USER_NAME} ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/${USER_NAME} \
 && mkdir -p /work /ksp /opt/parttools \
 && chown ${USER_UID}:${USER_GID} /work /opt/parttools

# A stable machine id for the license binding (see docs/license.md). It is
# fixed per image build; rebuilding the image may require reactivating.
RUN [ -s /etc/machine-id ] || dbus-uuidgen > /etc/machine-id

COPY bin/ /usr/local/bin/
COPY openbox/menu.xml /etc/xdg/openbox/menu.xml
RUN chmod +x /usr/local/bin/*

# Starts as root; moddev-entrypoint remaps modder to PUID/PGID and drops to it.
WORKDIR /work
ENTRYPOINT ["moddev-entrypoint"]
ENV HOME=/home/${USER_NAME} \
    DISPLAY=:1 \
    BROWSER=firefox \
    MOZ_DISABLE_CONTENT_SANDBOX=1 \
    MOZ_DISABLE_GMP_SANDBOX=1 \
    MOZ_DISABLE_RDD_SANDBOX=1 \
    MOZ_DISABLE_SOCKET_PROCESS_SANDBOX=1 \
    UNITY_EDITOR=/opt/unity/Editor/Unity \
    PARTTOOLS_DIR=/opt/parttools \
    KSP_DIR=/ksp

EXPOSE 6080
CMD ["bash"]
