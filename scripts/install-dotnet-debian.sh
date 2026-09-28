#!/usr/bin/env bash
# Installe le SDK .NET 10 (LTS) depuis le dépôt Microsoft, sur Debian 12 ou 13.
set -euo pipefail

. /etc/os-release
if [[ "${ID}" != "debian" ]]; then
  echo "Ce script vise Debian (détecté : ${ID})." >&2
  exit 1
fi

tmp="$(mktemp -d)"
wget -q "https://packages.microsoft.com/config/debian/${VERSION_ID}/packages-microsoft-prod.deb" -O "${tmp}/ms.deb"
sudo dpkg -i "${tmp}/ms.deb"
rm -rf "${tmp}"

sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

dotnet --info | head -n 5
