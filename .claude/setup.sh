#!/usr/bin/env bash
# Makes sure the .NET SDK this project needs is installed and on PATH.
#
# Cloud and CI containers often start without any .NET at all, and the first thing anyone does here is
# try to build. Installing it up front is faster than discovering it is missing halfway through a task.
# Idempotent: re-running when the SDK is already present costs a version check and nothing else.

set -euo pipefail

readonly DOTNET_CHANNEL="10.0"
readonly INSTALL_DIR="${HOME}/.dotnet"

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    echo "dotnet $(dotnet --version) already available."
    exit 0
fi

echo "Installing the .NET ${DOTNET_CHANNEL} SDK into ${INSTALL_DIR}..."

script="$(mktemp)"
trap 'rm -f "${script}"' EXIT

curl -sSL --retry 3 -o "${script}" https://dot.net/v1/dotnet-install.sh
bash "${script}" --channel "${DOTNET_CHANNEL}" --install-dir "${INSTALL_DIR}" >/dev/null

# The SDK lives in a home directory that is not on PATH, and the apphost that `dotnet build` produces
# needs DOTNET_ROOT to find its runtime. Set both so plain `dotnet` and the built binary each work.
if [ -w /usr/local/bin ]; then
    ln -sf "${INSTALL_DIR}/dotnet" /usr/local/bin/dotnet
fi

if [ -w /etc/profile.d ]; then
    printf 'export DOTNET_ROOT=%s\nexport PATH="$DOTNET_ROOT:$PATH"\n' "${INSTALL_DIR}" > /etc/profile.d/dotnet.sh
fi

export DOTNET_ROOT="${INSTALL_DIR}"
export PATH="${INSTALL_DIR}:${PATH}"

echo "Installed dotnet $(dotnet --version)."
echo "If 'dotnet' is not found in a shell, use: export DOTNET_ROOT=${INSTALL_DIR} PATH=\"${INSTALL_DIR}:\$PATH\""
