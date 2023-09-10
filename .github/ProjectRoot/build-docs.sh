#!/bin/bash

set -euxo pipefail

apt-get -y update
apt-get -y install dotnet7
dotnet tool update -g docfx
ls -l /root/.dotnet/tools