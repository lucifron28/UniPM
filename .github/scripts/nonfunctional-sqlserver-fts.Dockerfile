FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
RUN set -eux; \
    . /etc/os-release; \
    test "$ID" = ubuntu; \
    apt-get update; \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      apt-transport-https ca-certificates curl gnupg; \
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | apt-key add -; \
    curl -fsSL "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/mssql-server-2022.list" \
      -o /etc/apt/sources.list.d/mssql-server-2022.list; \
    apt-get update; \
    ACCEPT_EULA=Y DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends mssql-server-fts; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

USER mssql
