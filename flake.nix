{
  description = "SourceGit, a graphical Git client";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-26.05";
    self.submodules = true;
  };

  outputs =
    { self, nixpkgs }:
    let
      forSystems = nixpkgs.lib.genAttrs [ "x86_64-linux" ];
    in
    {
      packages = forSystems (
        system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          sourcegit = pkgs.callPackage ./nix/package.nix { src = self.outPath; };
        in
        {
          inherit sourcegit;
          default = sourcegit;
        }
      );

      apps = forSystems (system: {
        default = {
          type = "app";
          program = "${self.packages.${system}.sourcegit}/bin/SourceGit";
          meta.description = "Launch SourceGit";
        };
      });

      checks = forSystems (system: { inherit (self.packages.${system}) sourcegit; });
    };
}
