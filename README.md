# ksp-moddev

A Docker environment for developing **Kerbal Space Program 1.12** mods without
installing Unity or Unity Hub on your system:

- **Unity 2019.4.18f1** (the exact version KSP 1.12 runs on), with Linux and
  Windows build targets, based on GameCI's [`unityci/editor`](https://hub.docker.com/r/unityci/editor) image
- **Unity Hub + Firefox**, only to activate the free Personal license
- **A virtual desktop in your browser** (Xvfb + openbox + x11vnc + noVNC). It works
  over an SSH tunnel and needs no GPU (software OpenGL through llvmpipe)
- **.NET SDK 8** to build mod DLLs (`net48`) against the game's own DLLs
- **Squad's official PartTools for 1.12**, downloaded on demand and verified by SHA-256

Nothing from Squad/Take-Two is inside the image. The game's DLLs are mounted
read-only (`/ksp`), and PartTools is downloaded on first use (`fetch-parttools`)
from web.archive.org copies, since the official link went dead in 2025.

## Quick start

```sh
docker compose build
docker compose run --rm moddev moddev-selftest   # checks everything, starts nothing
docker compose up -d                             # starts the virtual desktop
```

Desktop: <http://localhost:6080/vnc.html>. The port only listens on `127.0.0.1`.
From another machine, run `ssh -L 6080:localhost:6080 <host>` and open the same address.

**First time:** activate the Unity license, following [docs/license.md](docs/license.md).

Right-click the desktop for the menu (Terminal, Unity Hub, Unity Editor, Firefox),
or get a shell with `docker exec -it ksp-moddev bash`.

| command | what it does |
|---|---|
| `unityhub-gui` | opens Unity Hub (only needed to activate the license) |
| `moddev-license` | shows whether a license is activated |
| `unity-gui /work/<project>` | opens the editor on a project |
| `fetch-parttools` | downloads `PartTools_PackageForModders.unitypackage` into `/opt/parttools` |
| `moddev-selftest` | quick check of the image |

To add PartTools to a project: *Assets → Import Package → Custom Package…* →
`/opt/parttools/PartTools_PackageForModders.unitypackage`.

## Volumes

| in the container | source (default) | purpose |
|---|---|---|
| `/work` | `$KSP_WORK` = `/mnt/projects/projects/ksp` | your mod repos |
| `/ksp` (ro) | `$KSP_GAME` = Steam's KSP install | `KSP_Data/Managed/*.dll` to build against |
| `/home/modder` | volume `home` | Unity license, Hub config, caches |
| `/opt/parttools` | volume `parttools` | downloaded PartTools |

Optional variables, in a `.env` next to `compose.yml`: `KSP_WORK`, `KSP_GAME`,
`MODDEV_UID`/`MODDEV_GID` (default 1000; match the owner of your repos) and
`MODDEV_GEOMETRY` (default `1920x1080x24`).

## Building a mod DLL

Use an SDK-style project targeting `net48` that references the game's DLLs in `/ksp`:

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

Then run `dotnet build -c Release`.

## Licenses

The scripts and Dockerfile in this repo are MIT. The Unity Editor is under
Unity's terms (the Personal license needs activation with your own Unity ID).
The base image comes from [GameCI](https://game.ci). PartTools and the KSP DLLs
belong to Squad/Take-Two and are not redistributed here.
