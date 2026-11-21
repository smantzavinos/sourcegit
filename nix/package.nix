{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  copyDesktopItems,
  makeDesktopItem,
  fontconfig,
  libGL,
  libxcursor,
  libxext,
  libxi,
  libxrandr,
  git,
  xdg-utils,
  src,
}:

buildDotnetModule (finalAttrs: {
  pname = "sourcegit";
  version = lib.trim (builtins.readFile "${src}/VERSION");
  inherit src;

  projectFile = "src/SourceGit.csproj";
  nugetDeps = ./deps.json;
  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0;
  executables = [ "SourceGit" ];
  dotnetFlags = [
    "-p:DisableAOT=true"
    "-p:DisableUpdateDetection=true"
  ];

  nativeBuildInputs = [ copyDesktopItems ];
  runtimeDeps = [
    fontconfig
    libGL
    libxcursor
    libxext
    libxi
    libxrandr
  ];
  makeWrapperArgs = [ "--suffix PATH : ${lib.makeBinPath [ git xdg-utils ]}" ];

  desktopItems = [
    (makeDesktopItem {
      name = "sourcegit";
      desktopName = "SourceGit";
      exec = "SourceGit %u";
      icon = "sourcegit";
      categories = [ "Development" ];
      mimeTypes = [ "inode/directory" ];
      comment = finalAttrs.meta.description;
    })
  ];

  postInstall = ''
    install -Dm644 build/resources/_common/icons/sourcegit.png \
      "$out/share/icons/hicolor/256x256/apps/sourcegit.png"
  '';

  meta = {
    description = "Free and open-source Git GUI client";
    homepage = "https://github.com/sourcegit-org/sourcegit";
    license = lib.licenses.mit;
    mainProgram = "SourceGit";
    platforms = [ "x86_64-linux" ];
  };
})
