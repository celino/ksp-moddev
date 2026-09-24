# ksp-moddev — ambiente isolado pra desenvolver mods do KSP 1.12
#
# Unity 2019.4.18f1 (a versão exata do KSP 1.12) vem da imagem do GameCI, com o
# módulo Windows Mono pra gerar asset bundles pro alvo Windows (o que a maioria
# dos mods publica). Por cima: Unity Hub (só pra ativar a licença Personal),
# desktop virtual acessível pelo navegador (Xvfb + openbox + x11vnc + noVNC) e
# .NET SDK pra compilar as DLLs dos mods.
#
# Nada da Squad/Take-Two vai dentro da imagem: o PartTools é baixado em tempo de
# execução (bin/fetch-parttools) e as DLLs do jogo entram por volume (/ksp).

ARG UNITY_IMAGE=unityci/editor:ubuntu-2019.4.18f1-windows-mono-3.2.2
FROM ${UNITY_IMAGE}

ARG DEBIAN_FRONTEND=noninteractive
ARG USER_NAME=modder
ARG USER_UID=1000
ARG USER_GID=1000
ARG DOTNET_CHANNEL=8.0

# Desktop virtual + GL por software (llvmpipe) pro editor abrir sem GPU.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl gnupg git unzip xz-utils sudo less nano \
      xvfb x11vnc openbox xterm novnc websockify dbus-x11 \
      libgl1-mesa-dri libglu1-mesa mesa-utils \
      fonts-dejavu-core \
      libgbm1 libasound2 libnss3 libsecret-1-0 libxss1 libgtk-3-0 \
 && rm -rf /var/lib/apt/lists/*

# Unity Hub: só serve pra ativar a licença Personal (grava o Unity_lic.ulf no
# home, que fica num volume). Fica aqui dentro pra não tocar no sistema host.
RUN curl -fsSL https://hub.unity3d.com/linux/keys/public \
      | gpg --dearmor -o /usr/share/keyrings/unityhub.gpg \
 && echo "deb [signed-by=/usr/share/keyrings/unityhub.gpg] https://hub.unity3d.com/linux/repos/deb stable main" \
      > /etc/apt/sources.list.d/unityhub.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends unityhub \
 && rm -rf /var/lib/apt/lists/*

# .NET SDK pra compilar DLLs de mod (projetos SDK-style com net48 +
# Microsoft.NETFramework.ReferenceAssemblies funcionam no Linux).
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel ${DOTNET_CHANNEL} --install-dir /opt/dotnet \
 && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
 && rm /tmp/dotnet-install.sh
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Usuário com o mesmo UID/GID do dono dos repos, pra não bagunçar permissões
# nos volumes. Remove qualquer usuário que já ocupe esse UID na imagem base.
RUN existing="$(getent passwd ${USER_UID} | cut -d: -f1)"; \
    if [ -n "$existing" ] && [ "$existing" != "${USER_NAME}" ]; then userdel -r "$existing" || true; fi; \
    getent group ${USER_GID} >/dev/null || groupadd -g ${USER_GID} ${USER_NAME}; \
    useradd -m -u ${USER_UID} -g ${USER_GID} -s /bin/bash ${USER_NAME} \
 && echo "${USER_NAME} ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/${USER_NAME} \
 && mkdir -p /work /ksp /opt/parttools \
 && chown ${USER_UID}:${USER_GID} /work /opt/parttools

COPY bin/ /usr/local/bin/
RUN chmod +x /usr/local/bin/*

USER ${USER_NAME}
WORKDIR /work
ENV DISPLAY=:1 \
    UNITY_EDITOR=/opt/unity/Editor/Unity \
    PARTTOOLS_DIR=/opt/parttools \
    KSP_DIR=/ksp

EXPOSE 6080
CMD ["bash"]
