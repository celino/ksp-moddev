# ksp-moddev

Ambiente em Docker pra desenvolver mods do **Kerbal Space Program 1.12** sem
instalar Unity/Unity Hub no sistema:

- **Unity 2019.4.18f1** (a versão exata do KSP 1.12), com alvos Linux e Windows,
  a partir da imagem [`unityci/editor`](https://hub.docker.com/r/unityci/editor) do GameCI
- **Unity Hub**, só pra ativar a licença Personal (gratuita)
- **Desktop virtual no navegador** (Xvfb + openbox + x11vnc + noVNC); funciona
  por túnel SSH, sem GPU (OpenGL por software, llvmpipe)
- **.NET SDK 8** pra compilar DLLs de mod (`net48`) contra as DLLs do jogo
- **PartTools oficial da Squad pro 1.12**, baixado sob demanda e conferido por SHA-256

Nada da Squad/Take-Two vai dentro da imagem. As DLLs do jogo entram por volume
somente leitura (`/ksp`), e o PartTools é baixado na primeira execução
(`fetch-parttools`) de cópias do web.archive.org, porque o link oficial saiu do ar em 2025.

## Uso

```sh
docker compose build
docker compose run --rm moddev moddev-selftest   # confere tudo, sem subir nada
docker compose up -d                             # sobe o desktop (noVNC)
```

Desktop: <http://localhost:6080/vnc.html>. A porta só escuta em `127.0.0.1`;
de outra máquina, use `ssh -L 6080:localhost:6080 <host>` e abra o mesmo endereço.

Dentro do desktop (clique direito → Terminal, ou `docker exec -it ksp-moddev bash`):

| comando | o quê |
|---|---|
| `unityhub-gui` | abre o Hub; login + ativar licença Personal (uma vez; fica no volume `home`) |
| `unity-gui /work/<projeto>` | abre o editor no projeto |
| `fetch-parttools` | baixa `PartTools_PackageForModders.unitypackage` em `/opt/parttools` |
| `moddev-selftest` | checagem rápida da imagem |

Pra importar o PartTools num projeto: *Assets → Import Package → Custom Package…* →
`/opt/parttools/PartTools_PackageForModders.unitypackage`.

## Volumes

| no container | origem (padrão) | pra quê |
|---|---|---|
| `/work` | `$KSP_WORK` = `/mnt/projects/projects/ksp` | seus repos de mods |
| `/ksp` (ro) | `$KSP_GAME` = instalação Steam do KSP | `KSP_Data/Managed/*.dll` pra compilar |
| `/home/modder` | volume `home` | licença Unity, config do Hub, caches |
| `/opt/parttools` | volume `parttools` | PartTools baixado |

Variáveis opcionais (num `.env` ao lado do `compose.yml`): `KSP_WORK`, `KSP_GAME`,
`MODDEV_UID`/`MODDEV_GID` (padrão 1000, igual ao dono dos repos) e `MODDEV_GEOMETRY`.

## Compilar uma DLL de mod

Projeto SDK-style mirando `net48`, referenciando as DLLs do jogo em `/ksp`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net48</TargetFramework><LangVersion>7.3</LangVersion></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <Reference Include="Assembly-CSharp"><HintPath>/ksp/KSP_Data/Managed/Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>/ksp/KSP_Data/Managed/UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

`dotnet build -c Release`.

## Licenças

Os scripts e o Dockerfile deste repo são MIT. O Unity Editor segue a licença da
Unity (a Personal exige ativação com conta própria); a imagem base é do
[GameCI](https://game.ci). O PartTools e as DLLs do KSP são da Squad/Take-Two e
não são redistribuídos aqui.
