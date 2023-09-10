#!/bin/bash

set -euxo pipefail

apt-get -y update
apt-get -y install dotnet7
(cd /; dotnet tool update -g docfx)
ls -l /root/.dotnet/tools
ls -l .

/root/.dotnet/tools/docfx Packages/nadena.dev.ndmf/docfx~/docfx.json
tar -C Packages/nadena.dev.ndmf/docfx~/_site -czf build/StandaloneWindows/docs.tgz .
